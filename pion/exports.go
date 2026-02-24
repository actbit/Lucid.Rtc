package main

/*
#include <stdint.h>
#include <stdlib.h>
*/
import "C"
import (
	"encoding/json"
	"sync"
	"unsafe"
)

var (
	clients     = make(map[int32]*Client)
	clientsMu   sync.Mutex
	nextClientID int32 = 1
)

//export lucid_rtc_create_client
func lucid_rtc_create_client(configJSON *C.char) int32 {
	var cfg string
	if configJSON != nil {
		cfg = C.GoString(configJSON)
	}

	client, err := NewClient(cfg)
	if err != nil {
		return 0
	}

	clientsMu.Lock()
	id := nextClientID
	nextClientID++
	clients[id] = client
	clientsMu.Unlock()

	return id
}

//export lucid_rtc_destroy_client
func lucid_rtc_destroy_client(clientID int32) {
	clientsMu.Lock()
	if client, ok := clients[clientID]; ok {
		client.Close()
		delete(clients, clientID)
	}
	clientsMu.Unlock()
}

//export lucid_rtc_create_offer
func lucid_rtc_create_offer(clientID int32, peerID *C.char) *C.char {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
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
func lucid_rtc_set_remote_offer(clientID int32, peerID, sdp *C.char) *C.char {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
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
func lucid_rtc_set_remote_answer(clientID int32, peerID, sdp *C.char) int32 {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
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
func lucid_rtc_add_ice_candidate(clientID int32, peerID, candidate, sdpMid *C.char, sdpMlineIndex int32) int32 {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
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
func lucid_rtc_send_message(clientID int32, peerID *C.char, data *C.uchar, len C.size_t) int32 {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
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
func lucid_rtc_poll_events(clientID int32) *C.char {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
		return nil
	}

	events := client.PollEvents()
	if len(events) == 0 {
		return C.CString("[]")
	}

	json, err := json.Marshal(events)
	if err != nil {
		return nil
	}

	return C.CString(string(json))
}

//export lucid_rtc_is_connected
func lucid_rtc_is_connected(clientID int32, peerID *C.char) int32 {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
		return -1
	}

	if client.IsConnected(C.GoString(peerID)) {
		return 1
	}
	return 0
}

//export lucid_rtc_wait_for_ice_connected
func lucid_rtc_wait_for_ice_connected(clientID int32, peerID *C.char) int32 {
	// Simple implementation - just check if connected
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
		return -1
	}

	// For now, just return success if connected
	// TODO: Implement proper waiting with timeout
	if client.IsConnected(C.GoString(peerID)) {
		return 0
	}
	return -1
}

//export lucid_rtc_close_peer
func lucid_rtc_close_peer(clientID int32, peerID *C.char) int32 {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
		return -1
	}

	err := client.ClosePeer(C.GoString(peerID))
	if err != nil {
		return -1
	}

	return 0
}

//export lucid_rtc_broadcast
func lucid_rtc_broadcast(clientID int32, data *C.uchar, len C.size_t) int32 {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
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
func lucid_rtc_close_all(clientID int32) int32 {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
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
	mediaClients     = make(map[int32]*MediaClient)
	mediaClientsMu   sync.Mutex
	nextMediaClientID int32 = 1000 // Start from different range
)

//export lucid_rtc_get_supported_codecs
func lucid_rtc_get_supported_codecs() *C.char {
	codecs := GetSupportedCodecs()
	json, err := json.Marshal(codecs)
	if err != nil {
		return nil
	}
	return C.CString(string(json))
}

//export lucid_rtc_create_media_track
func lucid_rtc_create_media_track(clientID int32, configJSON *C.char) *C.char {
	clientsMu.Lock()
	client, ok := clients[clientID]
	clientsMu.Unlock()

	if !ok {
		return nil
	}

	// Create or get media client
	mediaClientsMu.Lock()
	mediaClient, ok := mediaClients[clientID]
	if !ok {
		mediaClient = &MediaClient{
			Client: client,
			tracks: make(map[string]*MediaTrack),
		}
		mediaClients[clientID] = mediaClient
	}
	mediaClientsMu.Unlock()

	trackID, err := mediaClient.CreateMediaTrack(C.GoString(configJSON))
	if err != nil {
		return nil
	}

	return C.CString(trackID)
}

//export lucid_rtc_add_track_to_peer
func lucid_rtc_add_track_to_peer(clientID int32, peerID, trackID *C.char) int32 {
	mediaClientsMu.Lock()
	mediaClient, ok := mediaClients[clientID]
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
func lucid_rtc_send_media_data(clientID int32, trackID *C.char, data *C.uchar, len C.size_t) int32 {
	mediaClientsMu.Lock()
	mediaClient, ok := mediaClients[clientID]
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
func lucid_rtc_remove_media_track(clientID int32, trackID *C.char) int32 {
	mediaClientsMu.Lock()
	mediaClient, ok := mediaClients[clientID]
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
