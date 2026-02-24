package main

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"sync"
	"time"

	"github.com/pion/rtp"
	"github.com/pion/webrtc/v3"
)

// MediaTrack represents a media track (audio/video)
type MediaTrack struct {
	track      *webrtc.TrackLocalStaticRTP
	trackID    string
	kind       string // "audio" or "video"
	codec      string
}

// MediaConfig for creating media tracks
type MediaConfig struct {
	Kind       string `json:"kind"`        // "audio" or "video"
	Codec      string `json:"codec"`       // "opus", "vp8", "vp9", "h264", "av1"
	TrackID    string `json:"track_id"`    // optional custom track ID
	StreamID   string `json:"stream_id"`   // optional custom stream ID
}

// CodecCapability represents a codec capability
type CodecCapability struct {
	MimeType    string `json:"mime_type"`
	ClockRate   uint32 `json:"clock_rate"`
	Channels    uint16 `json:"channels,omitempty"`
	SDPFmtpLine string `json:"sdp_fmtp_line,omitempty"`
}

// MediaClient extends Client with media capabilities
type MediaClient struct {
	*Client
	tracks   map[string]*MediaTrack
	tracksMu sync.Mutex
}

// NewMediaClient creates a client with media support
func NewMediaClient(configJSON string) (*MediaClient, error) {
	client, err := NewClient(configJSON)
	if err != nil {
		return nil, err
	}

	return &MediaClient{
		Client: client,
		tracks: make(map[string]*MediaTrack),
	}, nil
}

// CreateMediaTrack creates a new media track for sending
func (mc *MediaClient) CreateMediaTrack(configJSON string) (string, error) {
	var config MediaConfig
	if err := json.Unmarshal([]byte(configJSON), &config); err != nil {
		return "", err
	}

	// Determine MIME type
	var mimeType string
	switch config.Kind {
	case "audio":
		switch config.Codec {
		case "opus":
			mimeType = webrtc.MimeTypeOpus
		case "g722":
			mimeType = webrtc.MimeTypeG722
		case "pcmu":
			mimeType = webrtc.MimeTypePCMU
		case "pcma":
			mimeType = webrtc.MimeTypePCMA
		default:
			mimeType = webrtc.MimeTypeOpus
		}
	case "video":
		switch config.Codec {
		case "vp8":
			mimeType = webrtc.MimeTypeVP8
		case "vp9":
			mimeType = webrtc.MimeTypeVP9
		case "h264":
			mimeType = webrtc.MimeTypeH264
		case "av1":
			mimeType = webrtc.MimeTypeAV1
		default:
			mimeType = webrtc.MimeTypeVP8
		}
	default:
		return "", &ClientError{Msg: "invalid kind, must be 'audio' or 'video'"}
	}

	// Generate track/stream IDs if not provided
	trackID := config.TrackID
	if trackID == "" {
		trackID = config.Kind + "_track_" + generateID()
	}
	streamID := config.StreamID
	if streamID == "" {
		streamID = "stream_" + generateID()
	}

	// Create local track
	track, err := webrtc.NewTrackLocalStaticRTP(
		webrtc.RTPCodecCapability{MimeType: mimeType},
		trackID,
		streamID,
	)
	if err != nil {
		return "", err
	}

	mediaTrack := &MediaTrack{
		track:   track,
		trackID: trackID,
		kind:    config.Kind,
		codec:   config.Codec,
	}

	mc.tracksMu.Lock()
	mc.tracks[trackID] = mediaTrack
	mc.tracksMu.Unlock()

	return trackID, nil
}

// AddTrackToPeer adds a media track to a peer connection
func (mc *MediaClient) AddTrackToPeer(peerID, trackID string) error {
	mc.tracksMu.Lock()
	mediaTrack, ok := mc.tracks[trackID]
	mc.tracksMu.Unlock()

	if !ok {
		return ErrTrackNotFound
	}

	mc.mu.RLock()
	peer, ok := mc.peers[peerID]
	mc.mu.RUnlock()

	if !ok {
		return ErrPeerNotFound
	}

	_, err := peer.pc.AddTrack(mediaTrack.track)
	return err
}

// SendMediaData sends RTP packets to a track
func (mc *MediaClient) SendMediaData(trackID string, data []byte) error {
	mc.tracksMu.Lock()
	mediaTrack, ok := mc.tracks[trackID]
	mc.tracksMu.Unlock()

	if !ok {
		return ErrTrackNotFound
	}

	// Create RTP packet
	packet := &rtp.Packet{
		Header: rtp.Header{
			Version:        2,
			PayloadType:    96, // Dynamic payload type
			SequenceNumber: 0,  // Will be set by track
			Timestamp:      0,  // Will be set by track
		},
		Payload: data,
	}

	return mediaTrack.track.WriteRTP(packet)
}

// RemoveMediaTrack removes a media track
func (mc *MediaClient) RemoveMediaTrack(trackID string) error {
	mc.tracksMu.Lock()
	defer mc.tracksMu.Unlock()

	if _, ok := mc.tracks[trackID]; !ok {
		return ErrTrackNotFound
	}

	delete(mc.tracks, trackID)
	return nil
}

// GetSupportedCodecs returns supported codecs
func GetSupportedCodecs() []CodecCapability {
	return []CodecCapability{
		// Audio codecs
		{MimeType: webrtc.MimeTypeOpus, ClockRate: 48000, Channels: 2},
		{MimeType: webrtc.MimeTypeG722, ClockRate: 8000},
		{MimeType: webrtc.MimeTypePCMU, ClockRate: 8000},
		{MimeType: webrtc.MimeTypePCMA, ClockRate: 8000},
		// Video codecs
		{MimeType: webrtc.MimeTypeVP8, ClockRate: 90000},
		{MimeType: webrtc.MimeTypeVP9, ClockRate: 90000},
		{MimeType: webrtc.MimeTypeH264, ClockRate: 90000},
		{MimeType: webrtc.MimeTypeAV1, ClockRate: 90000},
	}
}

// Error types for media
var (
	ErrTrackNotFound = &ClientError{Msg: "track not found"}
)

// Simple ID generator using crypto/rand
func generateID() string {
	b := make([]byte, 4)
	if _, err := rand.Read(b); err != nil {
		// Fallback to timestamp-based ID if crypto/rand fails
		return hex.EncodeToString([]byte(time.Now().String())[:8])
	}
	return hex.EncodeToString(b)
}
