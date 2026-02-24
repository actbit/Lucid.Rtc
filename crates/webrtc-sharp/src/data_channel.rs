//! Data channel handling.

use std::sync::Arc;
use tokio::sync::mpsc;
use webrtc::data_channel::RTCDataChannel;
use webrtc::data_channel::data_channel_state::RTCDataChannelState;

use crate::error::Result;
use crate::signaling::Event;

/// Configuration for data channel
#[derive(Debug, Clone)]
pub struct DataChannelConfig {
    /// Whether the channel is ordered
    pub ordered: bool,

    /// Max retransmits (for unreliable channels)
    pub max_retransmits: Option<u16>,
}

impl Default for DataChannelConfig {
    fn default() -> Self {
        Self {
            ordered: true,
            max_retransmits: None,
        }
    }
}

impl DataChannelConfig {
    /// Create a reliable (ordered) channel configuration
    pub fn reliable() -> Self {
        Self {
            ordered: true,
            max_retransmits: None,
        }
    }

    /// Create an unreliable (unordered) channel configuration
    pub fn unreliable(max_retransmits: Option<u16>) -> Self {
        Self {
            ordered: false,
            max_retransmits,
        }
    }
}

/// Handle for a data channel
pub struct DataChannelHandle {
    dc: Arc<RTCDataChannel>,
    peer_id: String,
    event_tx: mpsc::Sender<Event>,
}

impl DataChannelHandle {
    pub fn new(
        dc: Arc<RTCDataChannel>,
        peer_id: String,
        event_tx: mpsc::Sender<Event>,
    ) -> Self {
        let handle = Self {
            dc: dc.clone(),
            peer_id: peer_id.clone(),
            event_tx: event_tx.clone(),
        };

        // Set up message handler
        let peer_id_clone = peer_id.clone();
        let tx_clone = event_tx.clone();
        dc.on_message(Box::new(move |msg| {
            let peer_id = peer_id_clone.clone();
            let tx = tx_clone.clone();
            Box::pin(async move {
                let event = Event::MessageReceived {
                    peer_id,
                    data: msg.data.to_vec(),
                };
                let _ = tx.send(event).await;
            })
        }));

        // Set up open handler
        let peer_id_clone = peer_id;
        let tx_clone = event_tx;
        dc.on_open(Box::new(move || {
            let peer_id = peer_id_clone.clone();
            let tx = tx_clone.clone();
            Box::pin(async move {
                let event = Event::DataChannelOpen { peer_id };
                let _ = tx.send(event).await;
            })
        }));

        handle
    }

    /// Send data through the channel
    pub async fn send(&self, data: &[u8]) -> Result<()> {
        self.dc.send(&data.to_vec().into()).await?;
        Ok(())
    }

    /// Close the channel
    pub async fn close(&self) -> Result<()> {
        self.dc.close().await?;
        Ok(())
    }

    /// Check if the channel is open
    pub fn is_open(&self) -> bool {
        self.dc.ready_state() == RTCDataChannelState::Open
    }
}
