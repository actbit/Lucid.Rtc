//! Peer connection management.

use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;

use tokio::sync::{mpsc, Mutex, RwLock};
use webrtc::api::APIBuilder;
use webrtc::data_channel::RTCDataChannel;
use webrtc::ice_transport::ice_candidate::RTCIceCandidateInit;
use webrtc::ice_transport::ice_connection_state::RTCIceConnectionState;
use webrtc::peer_connection::configuration::RTCConfiguration;
use webrtc::peer_connection::sdp::session_description::RTCSessionDescription;

use crate::config::Config;
use crate::error::{Error, Result};
use crate::ice::create_ice_servers;
use crate::signaling::{Event, IceCandidate};

/// Internal peer state
struct PeerState {
    connection: Arc<webrtc::peer_connection::RTCPeerConnection>,
    data_channel: Option<Arc<RTCDataChannel>>,
}

/// WebRTC Client for peer-to-peer communication
pub struct WebRtcClient {
    config: Config,
    peers: Arc<RwLock<HashMap<String, PeerState>>>,
    event_tx: mpsc::Sender<Event>,
    event_rx: Arc<Mutex<mpsc::Receiver<Event>>>,
    api: Arc<webrtc::api::API>,
}

/// Builder for WebRtcClient
pub struct WebRtcClientBuilder {
    config: Config,
}

impl Default for WebRtcClientBuilder {
    fn default() -> Self {
        Self::new()
    }
}

impl WebRtcClientBuilder {
    /// Create a new builder with default configuration
    pub fn new() -> Self {
        Self {
            config: Config::default(),
        }
    }

    /// Set the configuration
    pub fn config(mut self, config: Config) -> Self {
        self.config = config;
        self
    }

    /// Add a STUN server
    pub fn add_stun_server(mut self, url: impl Into<String>) -> Self {
        self.config.stun_servers.push(url.into());
        self
    }

    /// Set TURN server
    pub fn turn_server(
        mut self,
        url: impl Into<String>,
        username: impl Into<String>,
        password: impl Into<String>,
    ) -> Self {
        self.config.turn_server = Some(crate::config::TurnConfig {
            url: url.into(),
            username: username.into(),
            password: password.into(),
        });
        self
    }

    /// Set ICE timeout in milliseconds
    pub fn ice_timeout_ms(mut self, timeout_ms: u64) -> Self {
        self.config.ice_timeout_ms = timeout_ms;
        self
    }

    /// Build the WebRTC client
    pub async fn build(self) -> Result<WebRtcClient> {
        WebRtcClient::new(self.config).await
    }
}

impl WebRtcClient {
    /// Create a new WebRTC client with the given configuration
    pub async fn new(config: Config) -> Result<Self> {
        let api = APIBuilder::new().build();

        let (event_tx, event_rx) = mpsc::channel(256);

        Ok(Self {
            config,
            peers: Arc::new(RwLock::new(HashMap::new())),
            event_tx,
            event_rx: Arc::new(Mutex::new(event_rx)),
            api: Arc::new(api),
        })
    }

    /// Create a new peer connection and generate an offer
    pub async fn create_offer(&self, peer_id: &str) -> Result<String> {
        let config = RTCConfiguration {
            ice_servers: create_ice_servers(&self.config),
            ..Default::default()
        };

        let peer_connection = self.api.new_peer_connection(config).await?;

        // Create data channel
        let dc = peer_connection
            .create_data_channel("data", None)
            .await?;

        // Set up data channel handlers
        let peer_id_owned = peer_id.to_string();
        let event_tx = self.event_tx.clone();
        dc.on_open(Box::new(move || {
            let peer_id = peer_id_owned.clone();
            let tx = event_tx.clone();
            Box::pin(async move {
                let _ = tx.send(Event::DataChannelOpen { peer_id }).await;
            })
        }));

        let peer_id_owned = peer_id.to_string();
        let event_tx = self.event_tx.clone();
        dc.on_message(Box::new(move |msg| {
            let peer_id = peer_id_owned.clone();
            let tx = event_tx.clone();
            Box::pin(async move {
                let _ = tx
                    .send(Event::MessageReceived {
                        peer_id,
                        data: msg.data.to_vec(),
                    })
                    .await;
            })
        }));

        let peer_id_owned = peer_id.to_string();
        let event_tx = self.event_tx.clone();
        dc.on_close(Box::new(move || {
            let peer_id = peer_id_owned.clone();
            let tx = event_tx.clone();
            Box::pin(async move {
                let _ = tx.send(Event::DataChannelClosed { peer_id }).await;
            })
        }));

        // Set up ICE candidate handler
        let peer_id_owned = peer_id.to_string();
        let event_tx = self.event_tx.clone();
        peer_connection.on_ice_candidate(Box::new(move |candidate| {
            let peer_id = peer_id_owned.clone();
            let tx = event_tx.clone();
            Box::pin(async move {
                if let Some(c) = candidate {
                    if let Ok(json) = c.to_json() {
                        let ice_candidate = IceCandidate {
                            candidate: json.candidate,
                            sdp_mid: json.sdp_mid.unwrap_or_default(),
                            sdp_mline_index: json.sdp_mline_index.unwrap_or(0) as u32,
                        };
                        let _ = tx.send(Event::IceCandidate {
                            peer_id,
                            candidate: ice_candidate,
                        }).await;
                    }
                }
            })
        }));

        // Set up ICE connection state handler
        let peer_id_owned = peer_id.to_string();
        let event_tx = self.event_tx.clone();
        peer_connection.on_ice_connection_state_change(Box::new(move |state| {
            let peer_id = peer_id_owned.clone();
            let tx = event_tx.clone();
            Box::pin(async move {
                let _ = tx
                    .send(Event::IceConnectionStateChange {
                        peer_id,
                        state: format!("{:?}", state),
                    })
                    .await;
            })
        }));

        // Create offer
        let offer = peer_connection.create_offer(None).await?;
        let sdp = offer.sdp.clone();

        // Set local description
        peer_connection.set_local_description(offer).await?;

        // Store peer state
        let mut peers = self.peers.write().await;
        peers.insert(
            peer_id.to_string(),
            PeerState {
                connection: Arc::new(peer_connection),
                data_channel: Some(dc),
            },
        );

        // Emit offer ready event
        let _ = self
            .event_tx
            .send(Event::OfferReady {
                peer_id: peer_id.to_string(),
                sdp: sdp.clone(),
            })
            .await;

        Ok(sdp)
    }

    /// Set remote offer and create an answer
    pub async fn set_remote_offer(&self, peer_id: &str, sdp: &str) -> Result<String> {
        let config = RTCConfiguration {
            ice_servers: create_ice_servers(&self.config),
            ..Default::default()
        };

        let peer_connection = self.api.new_peer_connection(config).await?;

        // Set up data channel handler for incoming channels
        let peer_id_owned = peer_id.to_string();
        let event_tx = self.event_tx.clone();
        peer_connection.on_data_channel(Box::new(move |dc| {
            let peer_id = peer_id_owned.clone();
            let tx = event_tx.clone();

            let peer_id_inner = peer_id.clone();
            let tx_inner = tx.clone();
            dc.on_open(Box::new(move || {
                let peer_id = peer_id_inner.clone();
                let tx = tx_inner.clone();
                Box::pin(async move {
                    let _ = tx.send(Event::DataChannelOpen { peer_id }).await;
                })
            }));

            let peer_id_inner = peer_id.clone();
            let tx_inner = tx.clone();
            dc.on_message(Box::new(move |msg| {
                let peer_id = peer_id_inner.clone();
                let tx = tx_inner.clone();
                Box::pin(async move {
                    let _ = tx
                        .send(Event::MessageReceived {
                            peer_id,
                            data: msg.data.to_vec(),
                        })
                        .await;
                })
            }));

            let peer_id_inner = peer_id.clone();
            let tx_inner = tx.clone();
            dc.on_close(Box::new(move || {
                let peer_id = peer_id_inner.clone();
                let tx = tx_inner.clone();
                Box::pin(async move {
                    let _ = tx.send(Event::DataChannelClosed { peer_id }).await;
                })
            }));

            Box::pin(async move {})
        }));

        // Set up ICE candidate handler
        let peer_id_owned = peer_id.to_string();
        let event_tx = self.event_tx.clone();
        peer_connection.on_ice_candidate(Box::new(move |candidate| {
            let peer_id = peer_id_owned.clone();
            let tx = event_tx.clone();
            Box::pin(async move {
                if let Some(c) = candidate {
                    if let Ok(json) = c.to_json() {
                        let ice_candidate = IceCandidate {
                            candidate: json.candidate,
                            sdp_mid: json.sdp_mid.unwrap_or_default(),
                            sdp_mline_index: json.sdp_mline_index.unwrap_or(0) as u32,
                        };
                        let _ = tx.send(Event::IceCandidate {
                            peer_id,
                            candidate: ice_candidate,
                        }).await;
                    }
                }
            })
        }));

        // Set up ICE connection state handler
        let peer_id_owned = peer_id.to_string();
        let event_tx = self.event_tx.clone();
        peer_connection.on_ice_connection_state_change(Box::new(move |state| {
            let peer_id = peer_id_owned.clone();
            let tx = event_tx.clone();
            Box::pin(async move {
                let _ = tx
                    .send(Event::IceConnectionStateChange {
                        peer_id,
                        state: format!("{:?}", state),
                    })
                    .await;
            })
        }));

        // Set remote description (offer)
        let offer = RTCSessionDescription::offer(sdp.to_string())?;
        peer_connection.set_remote_description(offer).await?;

        // Create answer
        let answer = peer_connection.create_answer(None).await?;
        let answer_sdp = answer.sdp.clone();

        // Set local description
        peer_connection.set_local_description(answer).await?;

        // Store peer state
        let mut peers = self.peers.write().await;
        peers.insert(
            peer_id.to_string(),
            PeerState {
                connection: Arc::new(peer_connection),
                data_channel: None, // Will be set when data channel opens
            },
        );

        // Emit answer ready event
        let _ = self
            .event_tx
            .send(Event::AnswerReady {
                peer_id: peer_id.to_string(),
                sdp: answer_sdp.clone(),
            })
            .await;

        Ok(answer_sdp)
    }

    /// Set remote answer (for the offerer)
    pub async fn set_remote_answer(&self, peer_id: &str, sdp: &str) -> Result<()> {
        let peers = self.peers.read().await;
        let peer_state = peers
            .get(peer_id)
            .ok_or_else(|| Error::PeerNotFound(peer_id.to_string()))?;

        let answer = RTCSessionDescription::answer(sdp.to_string())?;
        peer_state.connection.set_remote_description(answer).await?;

        // Emit peer connected event
        let _ = self
            .event_tx
            .send(Event::PeerConnected {
                peer_id: peer_id.to_string(),
            })
            .await;

        Ok(())
    }

    /// Add an ICE candidate
    pub async fn add_ice_candidate(
        &self,
        peer_id: &str,
        candidate: &IceCandidate,
    ) -> Result<()> {
        let peers = self.peers.read().await;
        let peer_state = peers
            .get(peer_id)
            .ok_or_else(|| Error::PeerNotFound(peer_id.to_string()))?;

        let init = RTCIceCandidateInit {
            candidate: candidate.candidate.clone(),
            sdp_mid: Some(candidate.sdp_mid.clone()),
            sdp_mline_index: Some(candidate.sdp_mline_index as u16),
            username_fragment: None,
        };

        peer_state.connection.add_ice_candidate(init).await?;

        Ok(())
    }

    /// Send a message to a peer
    pub async fn send_message(&self, peer_id: &str, data: &[u8]) -> Result<()> {
        let peers = self.peers.read().await;
        let peer_state = peers
            .get(peer_id)
            .ok_or_else(|| Error::PeerNotFound(peer_id.to_string()))?;

        if let Some(dc) = &peer_state.data_channel {
            dc.send(&data.to_vec().into()).await?;
        } else {
            return Err(Error::DataChannel("No data channel available".to_string()));
        }

        Ok(())
    }

    /// Send a message to all connected peers
    pub async fn broadcast(&self, data: &[u8]) -> Result<()> {
        let peers = self.peers.read().await;
        let mut errors = Vec::new();

        for (peer_id, peer_state) in peers.iter() {
            if let Some(dc) = &peer_state.data_channel {
                if let Err(e) = dc.send(&data.to_vec().into()).await {
                    errors.push(format!("{}: {}", peer_id, e));
                }
            }
        }

        if !errors.is_empty() {
            return Err(Error::DataChannel(format!(
                "Failed to send to some peers: {}",
                errors.join(", ")
            )));
        }

        Ok(())
    }

    /// Check if ICE connection is established
    pub async fn is_ice_connected(&self, peer_id: &str) -> bool {
        let peers = self.peers.read().await;
        if let Some(peer_state) = peers.get(peer_id) {
            let state = peer_state.connection.ice_connection_state();
            return state == RTCIceConnectionState::Connected
                || state == RTCIceConnectionState::Completed;
        }
        false
    }

    /// Wait for ICE connection to be established
    pub async fn wait_for_ice_connected(&self, peer_id: &str) -> Result<()> {
        let timeout = Duration::from_millis(self.config.ice_timeout_ms);
        let start = std::time::Instant::now();

        while start.elapsed() < timeout {
            if self.is_ice_connected(peer_id).await {
                return Ok(());
            }
            tokio::time::sleep(Duration::from_millis(100)).await;
        }

        Err(Error::IceTimeout)
    }

    /// Close a peer connection
    pub async fn close_peer(&self, peer_id: &str) -> Result<()> {
        let mut peers = self.peers.write().await;
        if let Some(peer_state) = peers.remove(peer_id) {
            peer_state.connection.close().await?;

            // Emit peer disconnected event
            let _ = self
                .event_tx
                .send(Event::PeerDisconnected {
                    peer_id: peer_id.to_string(),
                })
                .await;
        }
        Ok(())
    }

    /// Poll for events (non-blocking)
    pub async fn poll_events(&self) -> Vec<Event> {
        let mut events = Vec::new();
        let mut rx = self.event_rx.lock().await;

        while let Ok(event) = rx.try_recv() {
            events.push(event);
        }

        events
    }

    /// Get all pending events (blocking until at least one event)
    pub async fn recv_event(&self) -> Option<Event> {
        let mut rx = self.event_rx.lock().await;
        rx.recv().await
    }

    /// Close all peer connections
    pub async fn close(&self) -> Result<()> {
        let peer_ids: Vec<String> = {
            let peers = self.peers.read().await;
            peers.keys().cloned().collect()
        };

        for peer_id in peer_ids {
            self.close_peer(&peer_id).await?;
        }

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_client_creation() {
        let config = Config::default();
        let client = WebRtcClient::new(config).await;
        assert!(client.is_ok());
    }

    #[tokio::test]
    async fn test_builder() {
        let client = WebRtcClientBuilder::new()
            .add_stun_server("stun:stun.example.com:3478")
            .ice_timeout_ms(10000)
            .build()
            .await;

        assert!(client.is_ok());
    }
}
