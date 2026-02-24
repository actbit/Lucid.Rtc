//! Data channel handling.

use std::sync::Arc;
use webrtc::data_channel::RTCDataChannel;
use webrtc::data_channel::data_channel_state::RTCDataChannelState;

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

/// Check if a data channel is open
pub fn is_channel_open(dc: &Arc<RTCDataChannel>) -> bool {
    dc.ready_state() == RTCDataChannelState::Open
}
