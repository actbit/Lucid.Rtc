//! Helper functions for FFI.

use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::ptr;

/// Convert a Rust string to a C string pointer.
///
/// The returned pointer must be freed by the caller using webrtc_sharp_free_string.
pub fn string_to_c_ptr(s: &str) -> *mut c_char {
    match CString::new(s) {
        Ok(c_string) => c_string.into_raw(),
        Err(_) => ptr::null_mut(),
    }
}

/// Convert a C string pointer to a Rust string.
///
/// # Safety
/// The pointer must be a valid null-terminated UTF-8 string.
pub unsafe fn c_ptr_to_string(ptr: *const c_char) -> Option<String> {
    if ptr.is_null() {
        return None;
    }
    CStr::from_ptr(ptr).to_str().ok().map(|s| s.to_string())
}
