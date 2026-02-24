//! C FFI bindings for Lucid.Rtc.
//!
//! This library provides a C-compatible API for the Lucid.Rtc WebRTC library.

use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::ptr;
use std::slice;

use tokio::runtime::Runtime;
use lucid_rtc::prelude::*;

mod helpers;

/// Opaque handle to WebRTC client
pub struct LucidRtcClientHandle {
    inner: WebRtcClient,
    runtime: Runtime,
}

/// Create a new WebRTC client.
///
/// # Safety
/// - config_json must be a valid null-terminated UTF-8 string
/// - Caller must free the returned handle using lucid_rtc_destroy_client
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_create_client(config_json: *const c_char) -> *mut LucidRtcClientHandle {
    if config_json.is_null() {
        return ptr::null_mut();
    }

    let config_str = match CStr::from_ptr(config_json).to_str() {
        Ok(s) => s,
        Err(_) => return ptr::null_mut(),
    };

    let config: Config = if config_str.is_empty() {
        Config::default()
    } else {
        match serde_json::from_str(config_str) {
            Ok(c) => c,
            Err(_) => return ptr::null_mut(),
        }
    };

    let runtime = match Runtime::new() {
        Ok(rt) => rt,
        Err(_) => return ptr::null_mut(),
    };

    let client = match runtime.block_on(async { WebRtcClient::new(config).await }) {
        Ok(c) => c,
        Err(_) => return ptr::null_mut(),
    };

    Box::into_raw(Box::new(LucidRtcClientHandle {
        inner: client,
        runtime,
    }))
}

/// Destroy a WebRTC client handle.
///
/// # Safety
/// - handle must be a valid pointer returned by lucid_rtc_create_client
/// - handle must not be used after this call
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_destroy_client(handle: *mut LucidRtcClientHandle) {
    if handle.is_null() {
        return;
    }

    let handle = Box::from_raw(handle);
    handle.runtime.block_on(async {
        let _ = handle.inner.close().await;
    });
}

/// Create an offer for a peer connection.
///
/// Returns a JSON string containing the SDP offer.
/// Caller must free the returned string using lucid_rtc_free_string.
///
/// # Safety
/// - handle must be a valid pointer
/// - peer_id must be a valid null-terminated UTF-8 string
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_create_offer(
    handle: *mut LucidRtcClientHandle,
    peer_id: *const c_char,
) -> *mut c_char {
    if handle.is_null() || peer_id.is_null() {
        return ptr::null_mut();
    }

    let handle = &*handle;
    let peer_id_str = match CStr::from_ptr(peer_id).to_str() {
        Ok(s) => s,
        Err(_) => return ptr::null_mut(),
    };

    handle.runtime.block_on(async {
        match handle.inner.create_offer(peer_id_str).await {
            Ok(sdp) => helpers::string_to_c_ptr(&sdp),
            Err(_) => ptr::null_mut(),
        }
    })
}

/// Set remote offer and create an answer.
///
/// Returns the SDP answer string.
/// Caller must free the returned string using lucid_rtc_free_string.
///
/// # Safety
/// - handle must be a valid pointer
/// - peer_id and sdp must be valid null-terminated UTF-8 strings
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_set_remote_offer(
    handle: *mut LucidRtcClientHandle,
    peer_id: *const c_char,
    sdp: *const c_char,
) -> *mut c_char {
    if handle.is_null() || peer_id.is_null() || sdp.is_null() {
        return ptr::null_mut();
    }

    let handle = &*handle;
    let peer_id_str = match CStr::from_ptr(peer_id).to_str() {
        Ok(s) => s,
        Err(_) => return ptr::null_mut(),
    };
    let sdp_str = match CStr::from_ptr(sdp).to_str() {
        Ok(s) => s,
        Err(_) => return ptr::null_mut(),
    };

    handle.runtime.block_on(async {
        match handle.inner.set_remote_offer(peer_id_str, sdp_str).await {
            Ok(answer_sdp) => helpers::string_to_c_ptr(&answer_sdp),
            Err(_) => ptr::null_mut(),
        }
    })
}

/// Set remote answer (for offerer).
///
/// Returns 0 on success, -1 on error.
///
/// # Safety
/// - handle must be a valid pointer
/// - peer_id and sdp must be valid null-terminated UTF-8 strings
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_set_remote_answer(
    handle: *mut LucidRtcClientHandle,
    peer_id: *const c_char,
    sdp: *const c_char,
) -> i32 {
    if handle.is_null() || peer_id.is_null() || sdp.is_null() {
        return -1;
    }

    let handle = &*handle;
    let peer_id_str = match CStr::from_ptr(peer_id).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    let sdp_str = match CStr::from_ptr(sdp).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };

    handle.runtime.block_on(async {
        match handle.inner.set_remote_answer(peer_id_str, sdp_str).await {
            Ok(()) => 0,
            Err(_) => -1,
        }
    })
}

/// Add an ICE candidate.
///
/// Returns 0 on success, -1 on error.
///
/// # Safety
/// - handle must be a valid pointer
/// - peer_id, candidate, and sdp_mid must be valid null-terminated UTF-8 strings
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_add_ice_candidate(
    handle: *mut LucidRtcClientHandle,
    peer_id: *const c_char,
    candidate: *const c_char,
    sdp_mid: *const c_char,
    sdp_mline_index: i32,
) -> i32 {
    if handle.is_null() || peer_id.is_null() || candidate.is_null() || sdp_mid.is_null() {
        return -1;
    }

    let handle = &*handle;
    let peer_id_str = match CStr::from_ptr(peer_id).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    let candidate_str = match CStr::from_ptr(candidate).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    let sdp_mid_str = match CStr::from_ptr(sdp_mid).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };

    handle.runtime.block_on(async {
        let ice_candidate = IceCandidate {
            candidate: candidate_str.to_string(),
            sdp_mid: sdp_mid_str.to_string(),
            sdp_mline_index: sdp_mline_index as u32,
        };
        match handle.inner.add_ice_candidate(peer_id_str, &ice_candidate).await {
            Ok(()) => 0,
            Err(_) => -1,
        }
    })
}

/// Send a message to a peer.
///
/// Returns 0 on success, -1 on error.
///
/// # Safety
/// - handle must be a valid pointer
/// - peer_id must be a valid null-terminated UTF-8 string
/// - data must be a valid pointer to len bytes
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_send_message(
    handle: *mut LucidRtcClientHandle,
    peer_id: *const c_char,
    data: *const u8,
    len: usize,
) -> i32 {
    if handle.is_null() || peer_id.is_null() || data.is_null() {
        return -1;
    }

    let handle = &*handle;
    let peer_id_str = match CStr::from_ptr(peer_id).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    let data_slice = slice::from_raw_parts(data, len);

    handle.runtime.block_on(async {
        match handle.inner.send_message(peer_id_str, data_slice).await {
            Ok(()) => 0,
            Err(_) => -1,
        }
    })
}

/// Poll for events.
///
/// Returns a JSON array of events.
/// Caller must free the returned string using lucid_rtc_free_string.
///
/// # Safety
/// - handle must be a valid pointer
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_poll_events(handle: *mut LucidRtcClientHandle) -> *mut c_char {
    if handle.is_null() {
        return ptr::null_mut();
    }

    let handle = &*handle;

    handle.runtime.block_on(async {
        let events = handle.inner.poll_events().await;
        match serde_json::to_string(&events) {
            Ok(json) => helpers::string_to_c_ptr(&json),
            Err(_) => ptr::null_mut(),
        }
    })
}

/// Check if a peer is connected.
///
/// Returns 1 if connected, 0 if not, -1 on error.
///
/// # Safety
/// - handle must be a valid pointer
/// - peer_id must be a valid null-terminated UTF-8 string
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_is_connected(
    handle: *mut LucidRtcClientHandle,
    peer_id: *const c_char,
) -> i32 {
    if handle.is_null() || peer_id.is_null() {
        return -1;
    }

    let handle = &*handle;
    let peer_id_str = match CStr::from_ptr(peer_id).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };

    handle.runtime.block_on(async {
        if handle.inner.is_ice_connected(peer_id_str).await {
            1
        } else {
            0
        }
    })
}

/// Wait for ICE connection.
///
/// Returns 0 on success, -1 on timeout or error.
///
/// # Safety
/// - handle must be a valid pointer
/// - peer_id must be a valid null-terminated UTF-8 string
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_wait_for_ice_connected(
    handle: *mut LucidRtcClientHandle,
    peer_id: *const c_char,
) -> i32 {
    if handle.is_null() || peer_id.is_null() {
        return -1;
    }

    let handle = &*handle;
    let peer_id_str = match CStr::from_ptr(peer_id).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };

    handle.runtime.block_on(async {
        match handle.inner.wait_for_ice_connected(peer_id_str).await {
            Ok(()) => 0,
            Err(_) => -1,
        }
    })
}

/// Close a peer connection.
///
/// Returns 0 on success, -1 on error.
///
/// # Safety
/// - handle must be a valid pointer
/// - peer_id must be a valid null-terminated UTF-8 string
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_close_peer(
    handle: *mut LucidRtcClientHandle,
    peer_id: *const c_char,
) -> i32 {
    if handle.is_null() || peer_id.is_null() {
        return -1;
    }

    let handle = &*handle;
    let peer_id_str = match CStr::from_ptr(peer_id).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };

    handle.runtime.block_on(async {
        match handle.inner.close_peer(peer_id_str).await {
            Ok(()) => 0,
            Err(_) => -1,
        }
    })
}

/// Broadcast a message to all connected peers.
///
/// Returns 0 on success, -1 on error.
///
/// # Safety
/// - handle must be a valid pointer
/// - data must be a valid pointer to len bytes
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_broadcast(
    handle: *mut LucidRtcClientHandle,
    data: *const u8,
    len: usize,
) -> i32 {
    if handle.is_null() || data.is_null() {
        return -1;
    }

    let handle = &*handle;
    let data_slice = slice::from_raw_parts(data, len);

    handle.runtime.block_on(async {
        match handle.inner.broadcast(data_slice).await {
            Ok(()) => 0,
            Err(_) => -1,
        }
    })
}

/// Close all peer connections.
///
/// Returns 0 on success, -1 on error.
///
/// # Safety
/// - handle must be a valid pointer
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_close_all(
    handle: *mut LucidRtcClientHandle,
) -> i32 {
    if handle.is_null() {
        return -1;
    }

    let handle = &*handle;

    handle.runtime.block_on(async {
        match handle.inner.close().await {
            Ok(()) => 0,
            Err(_) => -1,
        }
    })
}

/// Free a string allocated by this library.
///
/// # Safety
/// - s must be a pointer returned by one of the lucid_rtc_* functions
/// - s must not be used after this call
#[no_mangle]
pub unsafe extern "C" fn lucid_rtc_free_string(s: *mut c_char) {
    if s.is_null() {
        return;
    }
    drop(CString::from_raw(s));
}

/// Get the version of the library.
///
/// Returns a version string (does not need to be freed).
#[no_mangle]
pub extern "C" fn lucid_rtc_version() -> *const c_char {
    static VERSION: &[u8] = b"0.1.0\0";
    VERSION.as_ptr() as *const c_char
}

// ============================================
// Media Functions (Rust backend stubs - not supported)
// These are only available in Pion backend
// ============================================

/// Get the backend type.
///
/// Returns "rust" for Rust backend.
#[no_mangle]
pub extern "C" fn lucid_rtc_get_backend() -> *const c_char {
    static BACKEND: &[u8] = b"rust\0";
    BACKEND.as_ptr() as *const c_char
}

/// Get supported codecs (stub - returns empty array for Rust backend).
#[no_mangle]
pub extern "C" fn lucid_rtc_get_supported_codecs() -> *mut c_char {
    // Return empty array - Rust backend doesn't support media
    helpers::string_to_c_ptr("[]")
}

/// Create media track (stub - not supported in Rust backend).
#[no_mangle]
pub extern "C" fn lucid_rtc_create_media_track(
    _handle: *mut LucidRtcClientHandle,
    _config_json: *const c_char,
) -> *mut c_char {
    // Return null - not supported
    ptr::null_mut()
}

/// Add track to peer (stub - not supported in Rust backend).
#[no_mangle]
pub extern "C" fn lucid_rtc_add_track_to_peer(
    _handle: *mut LucidRtcClientHandle,
    _peer_id: *const c_char,
    _track_id: *const c_char,
) -> i32 {
    // Return error - not supported
    -1
}

/// Send media data (stub - not supported in Rust backend).
#[no_mangle]
pub extern "C" fn lucid_rtc_send_media_data(
    _handle: *mut LucidRtcClientHandle,
    _track_id: *const c_char,
    _data: *const u8,
    _len: usize,
) -> i32 {
    // Return error - not supported
    -1
}

/// Remove media track (stub - not supported in Rust backend).
#[no_mangle]
pub extern "C" fn lucid_rtc_remove_media_track(
    _handle: *mut LucidRtcClientHandle,
    _track_id: *const c_char,
) -> i32 {
    // Return error - not supported
    -1
}
