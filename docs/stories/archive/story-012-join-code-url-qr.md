---
id: story-012
title: Player can join a room via URL or QR code
status: done
created: 2026-03-14
---

## What

A host can share a room link or QR code that takes other players directly into the room without typing a join code.

## Why

Sharing a link or scanning a QR code is faster and less error-prone than reading out a 6-character code, especially on mobile.

## Acceptance criteria

- [x] Every room has a shareable URL: `meepliton.com/join/{CODE}`
- [x] Visiting `/join/{CODE}` while signed in adds the user to the room and redirects to `/room/{roomId}`
- [x] Visiting `/join/{CODE}` while signed out redirects to sign-in, then back to the join URL after authentication
- [x] The room waiting screen displays the join URL as a clickable/copyable link
- [x] The room waiting screen displays a QR code generated client-side (using `qrcode.react` or equivalent) that encodes the join URL
- [x] The QR code and join URL are only visible on the waiting screen (not during an active game)
- [x] Visiting an expired or non-existent join code shows a friendly "Room not found" page

## Notes

- Spec: `docs/requirements.md` §4 (joining user stories), §5 (FR-ROOM-02 to FR-ROOM-04)
- Backend agent: `GET /api/rooms/join/{code}` redirect endpoint (or handled by frontend routing + `POST /api/rooms/join`)
- Frontend agent: QR code component, `/join/:code` route handler, copy-to-clipboard on join URL
- QR generation is client-side — no backend involvement for the code itself
- `qrcode.react` is already listed in the tech stack
