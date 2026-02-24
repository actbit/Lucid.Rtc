//! Error types for the WebRTC Sharp library.

use thiserror::Error;

pub type Result<T> = std::result::Result<T, Error>;

#[derive(Error, Debug)]
pub enum Error {
    #[error("WebRTC error: {0}")]
    WebRtc(#[from] webrtc::Error),

    #[error("ICE connection timeout")]
    IceTimeout,

    #[error("Peer not found: {0}")]
    PeerNotFound(String),

    #[error("Peer connection failed: {0}")]
    PeerConnectionFailed(String),

    #[error("Data channel error: {0}")]
    DataChannel(String),

    #[error("Signaling error: {0}")]
    Signaling(String),

    #[error("Invalid SDP: {0}")]
    InvalidSdp(String),

    #[error("Serialization error: {0}")]
    Serialization(#[from] serde_json::Error),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Channel closed")]
    ChannelClosed,

    #[error("Client not initialized")]
    NotInitialized,

    #[error("Invalid configuration: {0}")]
    InvalidConfig(String),

    #[error("Internal error: {0}")]
    Internal(String),
}

impl From<tokio::sync::mpsc::error::SendError<super::signaling::Event>> for Error {
    fn from(e: tokio::sync::mpsc::error::SendError<super::signaling::Event>) -> Self {
        Error::Internal(e.to_string())
    }
}
