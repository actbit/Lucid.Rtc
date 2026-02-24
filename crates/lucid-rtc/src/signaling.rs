//! Signaling types and event handling.

use serde::{Deserialize, Serialize, Serializer};

/// Serialize bytes as base64
fn serialize_base64<S>(data: &Vec<u8>, serializer: S) -> std::result::Result<S::Ok, S::Error>
where
    S: Serializer,
{
    use base64::engine::general_purpose::STANDARD;
    use base64::Engine;
    let encoded = STANDARD.encode(data);
    serializer.serialize_str(&encoded)
}

/// ICE candidate information
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct IceCandidate {
    /// The candidate string
    pub candidate: String,

    /// SDP media identifier
    pub sdp_mid: String,

    /// SDP media line index
    pub sdp_mline_index: u32,
}

/// Events emitted by the WebRTC client
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum Event {
    /// A peer has connected
    PeerConnected {
        peer_id: String,
    },

    /// A peer has disconnected
    PeerDisconnected {
        peer_id: String,
    },

    /// A message was received from a peer
    MessageReceived {
        peer_id: String,
        /// Use base64 for byte array serialization
        #[serde(serialize_with = "serialize_base64")]
        data: Vec<u8>,
    },

    /// An ICE candidate was generated
    IceCandidate {
        peer_id: String,
        candidate: IceCandidate,
    },

    /// SDP offer is ready
    OfferReady {
        peer_id: String,
        sdp: String,
    },

    /// SDP answer is ready
    AnswerReady {
        peer_id: String,
        sdp: String,
    },

    /// ICE connection state changed
    IceConnectionStateChange {
        peer_id: String,
        state: String,
    },

    /// An error occurred
    Error {
        #[serde(skip_serializing_if = "Option::is_none")]
        peer_id: Option<String>,
        message: String,
    },

    /// Data channel is open
    DataChannelOpen {
        peer_id: String,
    },

    /// Data channel is closed
    DataChannelClosed {
        peer_id: String,
    },
}
