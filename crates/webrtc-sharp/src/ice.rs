//! ICE (Interactive Connectivity Establishment) configuration and handling.

use webrtc::ice_transport::ice_server::RTCIceServer;

use crate::config::Config;

/// Create ICE servers from configuration
pub fn create_ice_servers(config: &Config) -> Vec<RTCIceServer> {
    let mut ice_servers = Vec::new();

    // Add STUN servers
    for stun_url in &config.stun_servers {
        ice_servers.push(RTCIceServer {
            urls: vec![stun_url.clone()],
            ..Default::default()
        });
    }

    // Add TURN server if configured
    if let Some(turn) = &config.turn_server {
        ice_servers.push(RTCIceServer {
            urls: vec![turn.url.clone()],
            username: turn.username.clone(),
            credential: turn.password.clone(),
            ..Default::default()
        });
    }

    ice_servers
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_create_ice_servers_with_stun() {
        let config = Config::default();
        let servers = create_ice_servers(&config);

        assert!(!servers.is_empty());
        assert_eq!(servers[0].urls.len(), 1);
    }

    #[test]
    fn test_create_ice_servers_with_turn() {
        let config = Config::builder()
            .add_stun_server("stun:stun.example.com:3478")
            .turn_server("turn:turn.example.com:3478", "user", "pass")
            .build();

        let servers = create_ice_servers(&config);

        assert_eq!(servers.len(), 2);
        assert_eq!(servers[1].username, "user");
        assert_eq!(servers[1].credential, "pass");
    }
}
