// Sumo Noodles matchmaking + relay server.
// Pairs players (quick match or private room code) and relays messages between the two.
// The first player of a pair is the "host": their browser runs the physics and streams snapshots.
"use strict";
const http = require("http");
const { WebSocketServer } = require("ws");

const PORT = process.env.PORT || 8080;
const PROTOCOL = 1;
const MAX_MSGS_PER_SEC = 90;
const CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

let nextId = 1;
const clients = new Set();
const rooms = new Map();        // code -> { code, players: [ws, ws?], private }
let waiting = null;             // one player waiting for a quick match

function send(ws, msg) { if (ws && ws.readyState === 1) ws.send(typeof msg === "string" ? msg : JSON.stringify(msg)); }
function newCode() {
  for (;;) {
    let c = "";
    for (let i = 0; i < 5; i++) c += CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)];
    if (!rooms.has(c)) return c;
  }
}
function pair(room) {
  const [a, b] = room.players;
  a.peer = b; b.peer = a;
  send(a, { t: "matched", role: "host", code: room.code, name: b.name });
  send(b, { t: "matched", role: "guest", code: room.code, name: a.name });
}
function leave(ws, notify) {
  if (waiting === ws) waiting = null;
  const room = ws.room;
  if (room) {
    room.players = room.players.filter(p => p !== ws);
    if (ws.peer) { if (notify) send(ws.peer, { t: "left" }); ws.peer.peer = null; ws.peer.room = null; }
    for (const p of room.players) p.room = null;
    rooms.delete(room.code);
  }
  ws.room = null; ws.peer = null;
}

const server = http.createServer((req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "*");
  if (req.url === "/stats") {
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ online: clients.size, searching: waiting ? 1 : 0, rooms: rooms.size }));
  } else { res.setHeader("Content-Type", "text/plain"); res.end("sumo noodles server ok"); }
});

const wss = new WebSocketServer({ server, maxPayload: 64 * 1024 });
wss.on("connection", ws => {
  ws.id = nextId++; ws.alive = true; ws.room = null; ws.peer = null; ws.name = "PLAYER"; ws.budget = MAX_MSGS_PER_SEC; ws.budgetT = Date.now();
  clients.add(ws);
  ws.on("pong", () => { ws.alive = true; });
  ws.on("message", (data, isBinary) => {
    const now = Date.now();
    if (now - ws.budgetT > 1000) { ws.budgetT = now; ws.budget = MAX_MSGS_PER_SEC; }
    if (--ws.budget < 0) return;                              // flood guard: drop
    const text = isBinary ? data.toString() : data.toString();
    // hot path: relay without parsing
    if (text.startsWith('{"t":"r"')) { if (ws.peer) send(ws.peer, text); return; }
    let m; try { m = JSON.parse(text); } catch (e) { return; }
    if (m.name) ws.name = String(m.name).replace(/[^\w .\-]/g, "").slice(0, 16).toUpperCase() || "PLAYER";
    switch (m.t) {
      case "hello":
        if (m.v !== PROTOCOL) send(ws, { t: "error", code: "version", msg: "Please reload the game to play online." });
        else send(ws, { t: "hi", online: clients.size });
        break;
      case "quick":
        leave(ws, true);
        if (waiting && waiting !== ws && waiting.readyState === 1) {
          const host = waiting; waiting = null;
          const room = { code: newCode(), players: [host, ws], private: false };
          rooms.set(room.code, room); host.room = room; ws.room = room;
          pair(room);
        } else { waiting = ws; send(ws, { t: "waiting" }); }
        break;
      case "create": {
        leave(ws, true);
        const room = { code: newCode(), players: [ws], private: true };
        rooms.set(room.code, room); ws.room = room;
        send(ws, { t: "room", code: room.code });
        break;
      }
      case "join": {
        const code = String(m.code || "").toUpperCase();
        const room = rooms.get(code);
        if (!room || room.players.length !== 1 || room.players[0] === ws) { send(ws, { t: "error", code: "noroom", msg: "That room is gone or already full." }); break; }
        leave(ws, true);
        room.players.push(ws); ws.room = room;
        pair(room);
        break;
      }
      case "leave": leave(ws, true); break;
    }
  });
  ws.on("close", () => { leave(ws, true); clients.delete(ws); });
  ws.on("error", () => {});
});

setInterval(() => {
  for (const ws of clients) {
    if (!ws.alive) { ws.terminate(); continue; }
    ws.alive = false; try { ws.ping(); } catch (e) {}
  }
}, 15000);

server.listen(PORT, () => console.log("sumo noodles server on :" + PORT));
