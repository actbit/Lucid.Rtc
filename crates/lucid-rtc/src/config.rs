//! Configuration types for WebRTC client.

use serde::{Deserialize, Serialize};

/// Main configuration for WebRTC client
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Config {
    /// STUN server URLs
    pub stun_servers: Vec<String>,

    /// TURN server configuration
    pub turn_server: Option<TurnConfig>,

    /// ICE connection timeout in milliseconds
    #[serde(default = "default_ice_timeout")]
    pub ice_timeout_ms: u64,

    /// Enable data channel reliability
    #[serde(default = "default_reliable")]
    pub data_channel_reliable: bool,
}

fn default_ice_timeout() -> u64 {
    30000 // 30 seconds
}

fn default_reliable() -> bool {
    true
}

impl Default for Config {
    fn default() -> Self {
        Self {
            stun_servers: vec![
                "stun:stun.l.google.com:19302".to_string(),
            ],
            turn_server: None,
            ice_timeout_ms: default_ice_timeout(),
            data_channel_reliable: default_reliable(),
        }
    }
}

/// TURN server configuration
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TurnConfig {
    /// TURN server URL
    pub url: String,

    /// Username for authentication
    pub username: String,

    /// Password for authentication
    pub password: String,
}

impl Config {
    /// Create a new configuration builder
    pub fn builder() -> ConfigBuilder {
        ConfigBuilder::default()
    }
}

/// Builder for Config
#[derive(Default)]
pub struct ConfigBuilder {
    config: Config,
}

impl ConfigBuilder {
    pub fn stun_servers(mut self, servers: Vec<String>) -> Self {
        self.config.stun_servers = servers;
        self
    }

    pub fn add_stun_server(mut self, url: impl Into<String>) -> Self {
        self.config.stun_servers.push(url.into());
        self
    }

    pub fn turn_server(mut self, url: impl Into<String>, username: impl Into<String>, password: impl Into<String>) -> Self {
        self.config.turn_server = Some(TurnConfig {
            url: url.into(),
            username: username.into(),
            password: password.into(),
        });
        self
    }

    pub fn ice_timeout_ms(mut self, timeout_ms: u64) -> Self {
        self.config.ice_timeout_ms = timeout_ms;
        self
    }

    pub fn data_channel_reliable(mut self, reliable: bool) -> Self {
        self.config.data_channel_reliable = reliable;
        self
    }

    pub fn build(self) -> Config {
        self.config
    }
}
