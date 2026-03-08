# Contract Changes: Remove Deprecated Code

**Date**: 2026-03-07  
**Feature**: Remove deprecated methods and properties  
**Status**: Design complete

## Overview

This directory documents the interface contracts affected by removing deprecated code. Since this is a **removal** feature, the contracts show what's being eliminated from the public API surface.

## Contract Files

### 1. [api-contract.md](api-contract.md)
Backend REST API contract changes for `SessionsController.Create` response (removes `linkToken` field).

### 2. [frontend-services-contract.md](frontend-services-contract.md)
Frontend service interface contracts after removing optional `linkToken` parameters.

## Backward Compatibility

**Breaking Changes**: ❌ YES (but acceptable)

- **Backend API**: `POST /api/sessions` response removes `linkToken` field
- **Frontend Services**: Remove optional `linkToken` parameters (breaking for any code passing this parameter)

**Justification**: 
- No external consumers depend on `linkToken` (Karamel-Web is a standalone application)
- LinkToken was already deprecated and functionally identical to AdminToken
- Frontend code already uses AdminToken/SingerToken exclusively
- See spec Edge Cases section: "Backward compatibility not required"

## Multi-Device Compatibility

✅ **No impact** - This cleanup does not affect multi-device functionality:
- AdminToken and SingerToken remain the primary authorization mechanism
- SignalR authorization works the same way (validated in PlaylistHub methods)
- QR code URLs continue to use `?session={guid}&token={adminToken}` (already did, now just removes deprecated linkToken option)

## Privacy & GDPR

✅ **Positive impact** - Removing LinkToken reduces data storage:
- One less token column in the database (minimal data principle)
- No new PII or sensitive data introduced
- Log messages updated to use AdminToken (which is already non-sensitive, public session identifier)
