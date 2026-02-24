//! WebRTC Sharp Library
//!
//! A high-level WebRTC library for peer-to-peer data channel communication.
//! Designed for .NET bindings via FFI.

pub mod peer;
pub mod data_channel;
pub mod ice;
pub mod signaling;
pub mod error;
pub mod config;

pub use peer::{WebRtcClient, WebRtcClientBuilder};
pub use error::{Error, Result};
pub use config::Config;
pub use signaling::Event;

/// Re-export commonly used types
pub mod prelude {
    pub use crate::peer::{WebRtcClient, WebRtcClientBuilder};
    pub use crate::config::Config;
    pub use crate::signaling::{Event, IceCandidate};
    pub use crate::error::{Error, Result};
}
