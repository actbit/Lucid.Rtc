package main

/*
#include <stdint.h>
#include <stdlib.h>
*/
import "C"
import (
	"encoding/json"
	"sync"
	"time"
	"unsafe"
)

// ClientHandle is an opaque handle for FFI
type ClientHandle struct {
	client *Client
	id     int64
}

var (
	clientHandles   = make(map[int64]*ClientHandle)
	clientHandlesMu sync.Mutex
	nextHandleID    int64 = 1
)

//export lucid_rtc_create_client
func lucid_rtc_create_client(configJSON *C.char) unsafe.Pointer {
	var cfg string
	if configJSON != nil {
		cfg = C.GoString(configJSON)
	}

	client, err := NewClient(cfg)
	if err != nil {
		return nil
	}

	clientHandlesMu.Lock()
	id := nextHandleID
	nextHandleID++
	handle := &ClientHandle{client: client, id: id}
	clientHandles[id] = handle
	clientHandlesMu.Unlock()

	// Return pointer to handle (like Rust)
	return unsafe.Pointer(handle)
}

//export lucid_rtc_destroy_client
func lucid_rtc_destroy_client(handle unsafe.Pointer) {
	if handle == nil {
		return
	}

	h := (*ClientHandle)(handle)
	clientHandlesMu.Lock()
	if _, ok := clientHandles[h.id]; ok {
		h.client.Close()
		delete(clientHandles, h.id)
	}
	clientHandlesMu.Unlock()
}

func getClient(handle unsafe.Pointer) *Client {
	if handle == nil {
		return nil
	}
	h := (*ClientHandle)(handle)
	return h.client
}

//export lucid_rtc_create_offer
func lucid_rtc_create_offer(handle unsafe.Pointer, peerID *C.char) *C.char {
	client := getClient(handle)
	if client == nil {
		return nil
	}

	peerIDStr := C.GoString(peerID)
	sdp, err := client.CreateOffer(peerIDStr)
	if err != nil {
		return nil
	}

	return C.CString(sdp)
}

//export lucid_rtc_set_remote_offer
func lucid_rtc_set_remote_offer(handle unsafe.Pointer, peerID, sdp *C.char) *C.char {
	client := getClient(handle)
	if client == nil {
		return nil
	}

	peerIDStr := C.GoString(peerID)
	sdpStr := C.GoString(sdp)
	answer, err := client.SetRemoteOffer(peerIDStr, sdpStr)
	if err != nil {
		return nil
	}

	return C.CString(answer)
}

//export lucid_rtc_set_remote_answer
func lucid_rtc_set_remote_answer(handle unsafe.Pointer, peerID, sdp *C.char) C.int {
	client := getClient(handle)
	if client == nil {
		return -1
	}

	peerIDStr := C.GoString(peerID)
	sdpStr := C.GoString(sdp)
	err := client.SetRemoteAnswer(peerIDStr, sdpStr)
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_add_ice_candidate
func lucid_rtc_add_ice_candidate(handle unsafe.Pointer, peerID, candidate, sdpMid *C.char, sdpMlineIndex C.int) C.int {
	client := getClient(handle)
	if client == nil {
		return -1
	}

	ice := IceCandidate{
		Candidate:     C.GoString(candidate),
		SdpMid:        C.GoString(sdpMid),
		SdpMlineIndex: int(sdpMlineIndex),
	}

	err := client.AddICECandidate(C.GoString(peerID), ice)
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_send_message
func lucid_rtc_send_message(handle unsafe.Pointer, peerID *C.char, data *C.uchar, len C.size_t) C.int {
	client := getClient(handle)
	if client == nil {
		return -1
	}

	// Convert C array to Go slice
	goData := C.GoBytes(unsafe.Pointer(data), C.int(len))

	err := client.SendMessage(C.GoString(peerID), goData)
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_poll_events
func lucid_rtc_poll_events(handle unsafe.Pointer) *C.char {
	client := getClient(handle)
	if client == nil {
		return nil
	}

	events := client.PollEvents()
	if len(events) == 0 {
		return C.CString("[]")
	}

	jsonData, err := json.Marshal(events)
	if err != nil {
		return nil
	}

	return C.CString(string(jsonData))
}

//export lucid_rtc_is_connected
func lucid_rtc_is_connected(handle unsafe.Pointer, peerID *C.char) C.int {
	client := getClient(handle)
	if client == nil {
		return -1
	}

	if client.IsConnected(C.GoString(peerID)) {
		return 1
	}
	return 0
}

//export lucid_rtc_wait_for_ice_connected
func lucid_rtc_wait_for_ice_connected(handle unsafe.Pointer, peerID *C.char) C.int {
	client := getClient(handle)
	if client == nil {
		return -1
	}

	peerIDStr := C.GoString(peerID)
	timeout := time.After(30 * time.Second)
	ticker := time.NewTicker(100 * time.Millisecond)
	defer ticker.Stop()

	for {
		select {
		case <-timeout:
			return -1
		case <-ticker.C:
			if client.IsConnected(peerIDStr) {
				return 0
			}
		}
	}
}

//export lucid_rtc_close_peer
func lucid_rtc_close_peer(handle unsafe.Pointer, peerID *C.char) C.int {
	client := getClient(handle)
	if client == nil {
		return -1
	}

	err := client.ClosePeer(C.GoString(peerID))
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_broadcast
func lucid_rtc_broadcast(handle unsafe.Pointer, data *C.uchar, len C.size_t) C.int {
	client := getClient(handle)
	if client == nil {
		return -1
	}

	goData := C.GoBytes(unsafe.Pointer(data), C.int(len))
	err := client.Broadcast(goData)
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_close_all
func lucid_rtc_close_all(handle unsafe.Pointer) C.int {
	client := getClient(handle)
	if client == nil {
		return -1
	}

	err := client.Close()
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_free_string
func lucid_rtc_free_string(s *C.char) {
	C.free(unsafe.Pointer(s))
}

//export lucid_rtc_version
func lucid_rtc_version() *C.char {
	// Return static string (caller should NOT free this)
	// This matches Rust behavior for consistency
	return (*C.char)(unsafe.Pointer(&versionBytes[0]))
}

// Static version string (null-terminated)
var versionBytes = [...]C.char{'0', '.', '1', '.', '0', '-', 'p', 'i', 'o', 'n', 0}

// ============================================
// Pion-specific Media Functions
// These are only available with Pion backend
// ============================================

var (
	mediaClients   = make(map[int64]*MediaClient)
	mediaClientsMu sync.Mutex
)

//export lucid_rtc_get_supported_codecs
func lucid_rtc_get_supported_codecs() *C.char {
	codecs := GetSupportedCodecs()
	jsonData, err := json.Marshal(codecs)
	if err != nil {
		return nil
	}
	return C.CString(string(jsonData))
}

//export lucid_rtc_create_media_track
func lucid_rtc_create_media_track(handle unsafe.Pointer, configJSON *C.char) *C.char {
	h := (*ClientHandle)(handle)
	if h == nil {
		return nil
	}

	clientHandlesMu.Lock()
	if _, ok := clientHandles[h.id]; !ok {
		clientHandlesMu.Unlock()
		return nil
	}
	clientHandlesMu.Unlock()

	// Create or get media client
	mediaClientsMu.Lock()
	mediaClient, ok := mediaClients[h.id]
	if !ok {
		mediaClient = &MediaClient{
			Client: h.client,
			tracks: make(map[string]*MediaTrack),
		}
		mediaClients[h.id] = mediaClient
	}
	mediaClientsMu.Unlock()

	trackID, err := mediaClient.CreateMediaTrack(C.GoString(configJSON))
	if err != nil {
		return nil
	}

	return C.CString(trackID)
}

//export lucid_rtc_add_track_to_peer
func lucid_rtc_add_track_to_peer(handle unsafe.Pointer, peerID, trackID *C.char) C.int {
	h := (*ClientHandle)(handle)
	if h == nil {
		return -1
	}

	mediaClientsMu.Lock()
	mediaClient, ok := mediaClients[h.id]
	mediaClientsMu.Unlock()

	if !ok {
		return -1
	}

	err := mediaClient.AddTrackToPeer(C.GoString(peerID), C.GoString(trackID))
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_send_media_data
func lucid_rtc_send_media_data(handle unsafe.Pointer, trackID *C.char, data *C.uchar, len C.size_t) C.int {
	h := (*ClientHandle)(handle)
	if h == nil {
		return -1
	}

	mediaClientsMu.Lock()
	mediaClient, ok := mediaClients[h.id]
	mediaClientsMu.Unlock()

	if !ok {
		return -1
	}

	goData := C.GoBytes(unsafe.Pointer(data), C.int(len))
	err := mediaClient.SendMediaData(C.GoString(trackID), goData)
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_remove_media_track
func lucid_rtc_remove_media_track(handle unsafe.Pointer, trackID *C.char) C.int {
	h := (*ClientHandle)(handle)
	if h == nil {
		return -1
	}

	mediaClientsMu.Lock()
	mediaClient, ok := mediaClients[h.id]
	mediaClientsMu.Unlock()

	if !ok {
		return -1
	}

	err := mediaClient.RemoveMediaTrack(C.GoString(trackID))
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_get_backend
func lucid_rtc_get_backend() *C.char {
	// Static string indicating the backend
	return (*C.char)(unsafe.Pointer(&backendBytes[0]))
}

var backendBytes = [...]C.char{'p', 'i', 'o', 'n', 0}

func main() {}
