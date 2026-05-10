# Remote Management API

Sietch Console embeds a lightweight web server (Kestrel) that exposes a REST and SSE API for remote monitoring and control of your battlegroup. This allows you to check server status, view live log output, and issue start/stop/restart commands from any browser on your local network — including a phone.

---

## Enabling Remote Management

1. Open Sietch Console and go to the **Settings** view.
2. Scroll to the **Remote Management** section.
3. Check **Enable remote web dashboard**.
4. Set the **Port** (default: 5151).
5. Enter or generate an **Access Token** (minimum 16 characters).
6. Click **Apply Remote Settings**.

The web dashboard will be available at `http://<your-LAN-IP>:<port>`. The Settings view shows the exact URL once the server is running.

> **Firewall note:** You may need to allow the port through Windows Firewall. The Networking tab's "Create Firewall Rule" button handles game ports; for the remote management port you may need to create a rule manually.

---

## Authentication

All API endpoints (except `GET /`) require a Bearer token. Set the `Authorization` header:

```
Authorization: Bearer <your-token>
```

### Rate limiting

After 5 failed authentication attempts from the same IP within 60 seconds, that IP is blocked for 5 minutes. Blocked requests receive HTTP `429 Too Many Requests` with a `Retry-After` header.

---

## Endpoints

### `GET /`

Returns the built-in web dashboard HTML page. No authentication required — the page handles auth in the browser using `sessionStorage`.

---

### `GET /api/status`

Returns the current server status.

**Response:**

```json
{
  "status": "Running",
  "uptimeSeconds": null,
  "playerCount": 0
}
```

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | One of: `Running`, `Starting`, `Stopping`, `Error`, `Offline` |
| `uptimeSeconds` | integer or null | Server uptime in seconds (null if not tracked) |
| `playerCount` | integer | Current player count (0 in the current release) |

---

### `POST /api/control/start`

Starts the battlegroup. Dispatches to the same service used by the WPF Dashboard.

**Response:** `202 Accepted`

---

### `POST /api/control/stop`

Stops the battlegroup gracefully.

**Response:** `202 Accepted`

---

### `POST /api/control/restart`

Restarts the battlegroup.

**Response:** `202 Accepted`

---

### `GET /api/logs?lines=50`

Returns the last N lines from the most recent server log file as a JSON array.

**Query parameters:**

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| `lines` | 50 | 1–500 | Number of lines to return |

**Response:**

```json
[
  {
    "timestamp": "14:32:01",
    "level": "Info",
    "message": "Server listening on port 7777"
  }
]
```

Returns an empty array if no log files exist yet.

---

### `GET /api/events`

An SSE (Server-Sent Events) stream that pushes real-time events to the client.

**Authentication:** Because `EventSource` cannot send custom headers, pass the token as a query parameter:

```
GET /api/events?token=<your-token>
```

**Event types:**

#### `status`

Emitted when the server status changes. Also emitted once on initial connection with the current state.

```
event: status
data: {"status":"Running","uptimeSeconds":null,"playerCount":0}
```

#### `log`

Emitted for each line of live server output (stdout/stderr from the server process).

```
event: log
data: {"timestamp":"14:32:05","level":"Info","message":"Player joined"}
```

---

## Web Dashboard

Visit `http://<LAN-IP>:<port>` in any browser to access the built-in dashboard. It features:

- **Status badge** — Running / Starting / Stopping / Error / Offline with the same color coding as the WPF app
- **Uptime and player count** at a glance
- **Start / Stop / Restart** controls (buttons are disabled when the action is unavailable)
- **Live log tail** — last 50 lines on load, then live-streamed via SSE
- **Real-time updates** via SSE — no polling required
- **Dark theme** matching the WPF app's color palette
- **Mobile-responsive** — usable on a phone browser

### Security model

- The token is stored in `sessionStorage`, not `localStorage` — it is cleared when the browser tab or window is closed.
- Closing the browser clears authentication; re-opening requires the token again.
- The token never appears in cookies or URLs for API calls — only the SSE endpoint uses `?token=` because `EventSource` does not support custom headers.

---

## Example: curl

```bash
# Check status
curl -H "Authorization: Bearer YOUR_TOKEN" http://192.168.1.10:5151/api/status

# Start server
curl -X POST -H "Authorization: Bearer YOUR_TOKEN" http://192.168.1.10:5151/api/control/start

# Tail logs
curl -H "Authorization: Bearer YOUR_TOKEN" "http://192.168.1.10:5151/api/logs?lines=20"
```

---

## Planned Improvements

- Player count from the server API when Funcom exposes it
- Server uptime tracking (start time persisted in SQLite)
- `GET /api/metrics` for CPU/memory data
- Optional HTTPS via a self-signed certificate
