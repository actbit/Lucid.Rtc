package main

import (
	"encoding/base64"
	"encoding/json"
	"sync"

	"github.com/pion/webrtc/v3"
)

// Event types matching Rust implementation (snake_case)
type EventType string

const (
	EventPeerConnected            EventType = "peer_connected"
	EventPeerDisconnected         EventType = "peer_disconnected"
	EventMessageReceived          EventType = "message_received"
	EventIceCandidate             EventType = "ice_candidate"
	EventError                    EventType = "error"
	EventDataChannelOpen          EventType = "data_channel_open"
	EventDataChannelClosed        EventType = "data_channel_closed"
	EventIceConnectionStateChange EventType = "ice_connection_state_change"
	EventOfferReady               EventType = "offer_ready"
	EventAnswerReady              EventType = "answer_ready"
	EventVideoFrame               EventType = "video_frame"
	EventAudioFrame               EventType = "audio_frame"
)

// RtcEvent represents an event from the WebRTC client
// Must match Rust's signaling.rs Event enum format
type RtcEvent struct {
	Type      EventType      `json:"type"`
	PeerId    string         `json:"peer_id,omitempty"`
	Data      string         `json:"data,omitempty"`      // base64 encoded for message_received
	Candidate *IceCandidate  `json:"candidate,omitempty"` // nested object for ice_candidate
	Sdp       string         `json:"sdp,omitempty"`
	State     string         `json:"state,omitempty"`
	Message   string         `json:"message,omitempty"` // for error events
}

// Sdp struct for offer/answer SDP
type Sdp struct {
	Type string `json:"type"`
	Sdp  string `json:"sdp"`
}

// IceCandidate represents an ICE candidate
type IceCandidate struct {
	Candidate     string `json:"candidate"`
	SdpMid        string `json:"sdp_mid"`
	SdpMlineIndex int    `json:"sdp_mline_index"`
}

// PeerConnection wraps a WebRTC peer connection
type PeerConnection struct {
	pc          *webrtc.PeerConnection
	dc          *webrtc.DataChannel
	peerId      string
	connected   bool
	events      []RtcEvent
	eventsMu    sync.Mutex
}

// Client is the main WebRTC client
type Client struct {
	mu       sync.RWMutex
	peers    map[string]*PeerConnection
	config   webrtc.Configuration
	events   []RtcEvent
	eventsMu sync.Mutex
}

// NewClient creates a new WebRTC client
func NewClient(configJSON string) (*Client, error) {
	config := webrtc.Configuration{
		ICEServers: []webrtc.ICEServer{
			{
				URLs: []string{"stun:stun.l.google.com:19302"},
			},
		},
	}

	if configJSON != "" {
		var cfg struct {
			StunServers []string `json:"stun_servers"`
			TurnServer  *struct {
				URL      string `json:"url"`
				Username string `json:"username"`
				Password string `json:"password"`
			} `json:"turn_server"`
		}
		if err := json.Unmarshal([]byte(configJSON), &cfg); err == nil {
			if len(cfg.StunServers) > 0 {
				config.ICEServers = []webrtc.ICEServer{}
				for _, s := range cfg.StunServers {
					config.ICEServers = append(config.ICEServers, webrtc.ICEServer{
						URLs: []string{s},
					})
				}
			}
			if cfg.TurnServer != nil {
				config.ICEServers = append(config.ICEServers, webrtc.ICEServer{
					URLs:           []string{cfg.TurnServer.URL},
					Username:       cfg.TurnServer.Username,
					Credential:     cfg.TurnServer.Password,
					CredentialType: webrtc.ICECredentialTypePassword,
				})
			}
		}
	}

	return &Client{
		peers:  make(map[string]*PeerConnection),
		config: config,
		events: make([]RtcEvent, 0),
	}, nil
}

// CreateOffer creates an SDP offer for a new peer
func (c *Client) CreateOffer(peerId string) (string, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	pc, err := webrtc.NewPeerConnection(c.config)
	if err != nil {
		return "", err
	}

	peer := &PeerConnection{
		pc:       pc,
		peerId:   peerId,
		events:   make([]RtcEvent, 0),
	}

	// Set up data channel handler
	pc.OnDataChannel(func(dc *webrtc.DataChannel) {
		peer.eventsMu.Lock()
		peer.dc = dc
		peer.eventsMu.Unlock()
		c.setupDataChannel(peer, dc)
	})

	// Set up ICE candidate handler
	pc.OnICECandidate(func(candidate *webrtc.ICECandidate) {
		if candidate != nil {
			candidateInit := candidate.ToJSON()
			sdpMid := ""
			sdpMlineIndex := 0
			if candidateInit.SDPMid != nil {
				sdpMid = *candidateInit.SDPMid
			}
			if candidateInit.SDPMLineIndex != nil {
				sdpMlineIndex = int(*candidateInit.SDPMLineIndex)
			}
			peer.eventsMu.Lock()
			peer.events = append(peer.events, RtcEvent{
				Type:   EventIceCandidate,
				PeerId: peerId,
				Candidate: &IceCandidate{
					Candidate:     candidateInit.Candidate,
					SdpMid:        sdpMid,
					SdpMlineIndex: sdpMlineIndex,
				},
			})
			peer.eventsMu.Unlock()
		}
	})

	// Set up ICE connection state handler
	pc.OnICEConnectionStateChange(func(state webrtc.ICEConnectionState) {
		peer.eventsMu.Lock()
		peer.events = append(peer.events, RtcEvent{
			Type:   EventIceConnectionStateChange,
			PeerId: peerId,
			State:  state.String(),
		})
		peer.eventsMu.Unlock()
	})

	// Set up track handler for receiving media
	pc.OnTrack(func(track *webrtc.TrackRemote, receiver *webrtc.RTPReceiver) {
		c.handleIncomingTrack(peer, track)
	})

	// Create data channel (offerer side)
	dc, err := pc.CreateDataChannel("data", nil)
	if err != nil {
		pc.Close()
		return "", err
	}
	peer.dc = dc
	c.setupDataChannel(peer, dc)

	// Create offer
	offer, err := pc.CreateOffer(nil)
	if err != nil {
		pc.Close()
		return "", err
	}

	if err := pc.SetLocalDescription(offer); err != nil {
		pc.Close()
		return "", err
	}

	// Only add to peers map after successful setup
	c.peers[peerId] = peer

	// Marshal SDP
	sdp, err := json.Marshal(offer)
	if err != nil {
		pc.Close()
		delete(c.peers, peerId)
		return "", err
	}

	// Emit offer_ready event
	peer.eventsMu.Lock()
	peer.events = append(peer.events, RtcEvent{
		Type:   EventOfferReady,
		PeerId: peerId,
		Sdp:    string(sdp),
	})
	peer.eventsMu.Unlock()

	return string(sdp), nil
}

// SetRemoteOffer sets the remote offer and creates an answer
func (c *Client) SetRemoteOffer(peerId, sdp string) (string, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	var offer webrtc.SessionDescription
	if err := json.Unmarshal([]byte(sdp), &offer); err != nil {
		return "", err
	}

	pc, err := webrtc.NewPeerConnection(c.config)
	if err != nil {
		return "", err
	}

	peer := &PeerConnection{
		pc:       pc,
		peerId:   peerId,
		events:   make([]RtcEvent, 0),
	}

	// Set up data channel handler (answerer side)
	pc.OnDataChannel(func(dc *webrtc.DataChannel) {
		peer.eventsMu.Lock()
		peer.dc = dc
		peer.eventsMu.Unlock()
		c.setupDataChannel(peer, dc)
	})

	// Set up ICE candidate handler
	pc.OnICECandidate(func(candidate *webrtc.ICECandidate) {
		if candidate != nil {
			candidateInit := candidate.ToJSON()
			sdpMid := ""
			sdpMlineIndex := 0
			if candidateInit.SDPMid != nil {
				sdpMid = *candidateInit.SDPMid
			}
			if candidateInit.SDPMLineIndex != nil {
				sdpMlineIndex = int(*candidateInit.SDPMLineIndex)
			}
			peer.eventsMu.Lock()
			peer.events = append(peer.events, RtcEvent{
				Type:   EventIceCandidate,
				PeerId: peerId,
				Candidate: &IceCandidate{
					Candidate:     candidateInit.Candidate,
					SdpMid:        sdpMid,
					SdpMlineIndex: sdpMlineIndex,
				},
			})
			peer.eventsMu.Unlock()
		}
	})

	// Set up ICE connection state handler
	pc.OnICEConnectionStateChange(func(state webrtc.ICEConnectionState) {
		peer.eventsMu.Lock()
		peer.events = append(peer.events, RtcEvent{
			Type:   EventIceConnectionStateChange,
			PeerId: peerId,
			State:  state.String(),
		})
		peer.eventsMu.Unlock()
	})

	// Set up track handler for receiving media
	pc.OnTrack(func(track *webrtc.TrackRemote, receiver *webrtc.RTPReceiver) {
		c.handleIncomingTrack(peer, track)
	})

	// Set remote description
	if err := pc.SetRemoteDescription(offer); err != nil {
		pc.Close()
		return "", err
	}

	// Create answer
	answer, err := pc.CreateAnswer(nil)
	if err != nil {
		pc.Close()
		return "", err
	}

	if err := pc.SetLocalDescription(answer); err != nil {
		pc.Close()
		return "", err
	}

	// Only add to peers map after successful setup
	c.peers[peerId] = peer

	// Marshal SDP
	answerSDP, err := json.Marshal(answer)
	if err != nil {
		pc.Close()
		delete(c.peers, peerId)
		return "", err
	}

	// Emit answer_ready event
	peer.eventsMu.Lock()
	peer.events = append(peer.events, RtcEvent{
		Type:   EventAnswerReady,
		PeerId: peerId,
		Sdp:    string(answerSDP),
	})
	peer.eventsMu.Unlock()

	return string(answerSDP), nil
}

// SetRemoteAnswer sets the remote answer (for offerer)
func (c *Client) SetRemoteAnswer(peerId, sdp string) error {
	c.mu.RLock()
	peer, ok := c.peers[peerId]
	c.mu.RUnlock()

	if !ok {
		return ErrPeerNotFound
	}

	var answer webrtc.SessionDescription
	if err := json.Unmarshal([]byte(sdp), &answer); err != nil {
		return err
	}

	return peer.pc.SetRemoteDescription(answer)
}

// AddICECandidate adds an ICE candidate
func (c *Client) AddICECandidate(peerId string, candidate IceCandidate) error {
	c.mu.RLock()
	peer, ok := c.peers[peerId]
	c.mu.RUnlock()

	if !ok {
		return ErrPeerNotFound
	}

	sdpMlineIndex := uint16(candidate.SdpMlineIndex)
	iceCandidate := webrtc.ICECandidateInit{
		Candidate:     candidate.Candidate,
		SDPMid:        &candidate.SdpMid,
		SDPMLineIndex: &sdpMlineIndex,
	}

	return peer.pc.AddICECandidate(iceCandidate)
}

// SendMessage sends a message to a peer
func (c *Client) SendMessage(peerId string, data []byte) error {
	c.mu.RLock()
	peer, ok := c.peers[peerId]
	c.mu.RUnlock()

	if !ok {
		return ErrPeerNotFound
	}

	peer.eventsMu.Lock()
	dc := peer.dc
	peer.eventsMu.Unlock()

	if dc == nil {
		return ErrDataChannelNotReady
	}

	return dc.Send(data)
}

// Broadcast sends a message to all connected peers
func (c *Client) Broadcast(data []byte) error {
	c.mu.RLock()
	defer c.mu.RUnlock()

	for _, peer := range c.peers {
		peer.eventsMu.Lock()
		dc := peer.dc
		peer.eventsMu.Unlock()

		if dc != nil {
			dc.Send(data)
		}
	}
	return nil
}

// IsConnected checks if a peer is connected
func (c *Client) IsConnected(peerId string) bool {
	c.mu.RLock()
	peer, ok := c.peers[peerId]
	c.mu.RUnlock()

	if !ok {
		return false
	}

	return peer.connected
}

// ClosePeer closes a peer connection
func (c *Client) ClosePeer(peerId string) error {
	c.mu.Lock()
	defer c.mu.Unlock()

	peer, ok := c.peers[peerId]
	if !ok {
		return ErrPeerNotFound
	}

	delete(c.peers, peerId)
	return peer.pc.Close()
}

// Close closes all peer connections
func (c *Client) Close() error {
	c.mu.Lock()
	defer c.mu.Unlock()

	for id, peer := range c.peers {
		peer.pc.Close()
		delete(c.peers, id)
	}
	return nil
}

// PollEvents returns all pending events
func (c *Client) PollEvents() []RtcEvent {
	c.mu.RLock()
	defer c.mu.RUnlock()

	var allEvents []RtcEvent
	for _, peer := range c.peers {
		peer.eventsMu.Lock()
		allEvents = append(allEvents, peer.events...)
		peer.events = make([]RtcEvent, 0)
		peer.eventsMu.Unlock()
	}

	return allEvents
}

// handleIncomingTrack processes incoming media tracks
func (c *Client) handleIncomingTrack(peer *PeerConnection, track *webrtc.TrackRemote) {
	kind := track.Kind().String()

	for {
		// Read RTP packets
		packet, _, err := track.ReadRTP()
		if err != nil {
			// Track closed or error
			peer.eventsMu.Lock()
			peer.events = append(peer.events, RtcEvent{
				Type:    EventError,
				PeerId:  peer.peerId,
				Message: "track read error: " + err.Error(),
			})
			peer.eventsMu.Unlock()
			return
		}

		// Determine event type based on track kind
		var eventType EventType
		if kind == "video" {
			eventType = EventVideoFrame
		} else if kind == "audio" {
			eventType = EventAudioFrame
		} else {
			continue // Unknown track kind
		}

		// Emit event with base64 encoded payload
		peer.eventsMu.Lock()
		peer.events = append(peer.events, RtcEvent{
			Type:   eventType,
			PeerId: peer.peerId,
			Data:   base64.StdEncoding.EncodeToString(packet.Payload),
		})
		peer.eventsMu.Unlock()
	}
}

func (c *Client) setupDataChannel(peer *PeerConnection, dc *webrtc.DataChannel) {
	dc.OnOpen(func() {
		peer.eventsMu.Lock()
		peer.connected = true
		peer.events = append(peer.events, RtcEvent{
			Type:   EventPeerConnected,
			PeerId: peer.peerId,
		})
		peer.events = append(peer.events, RtcEvent{
			Type:   EventDataChannelOpen,
			PeerId: peer.peerId,
		})
		peer.eventsMu.Unlock()
	})

	dc.OnClose(func() {
		peer.eventsMu.Lock()
		peer.connected = false
		peer.events = append(peer.events, RtcEvent{
			Type:   EventDataChannelClosed,
			PeerId: peer.peerId,
		})
		peer.events = append(peer.events, RtcEvent{
			Type:   EventPeerDisconnected,
			PeerId: peer.peerId,
		})
		peer.eventsMu.Unlock()
	})

	dc.OnMessage(func(msg webrtc.DataChannelMessage) {
		peer.eventsMu.Lock()
		peer.events = append(peer.events, RtcEvent{
			Type:   EventMessageReceived,
			PeerId: peer.peerId,
			Data:   base64.StdEncoding.EncodeToString(msg.Data),
		})
		peer.eventsMu.Unlock()
	})

	dc.OnError(func(err error) {
		peer.eventsMu.Lock()
		peer.events = append(peer.events, RtcEvent{
			Type:    EventError,
			Message: err.Error(),
		})
		peer.eventsMu.Unlock()
	})
}

// Error types
var (
	ErrPeerNotFound       = &ClientError{Msg: "peer not found"}
	ErrDataChannelNotReady = &ClientError{Msg: "data channel not ready"}
)

type ClientError struct {
	Msg string
}

func (e *ClientError) Error() string {
	return e.Msg
}
