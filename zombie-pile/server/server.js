// Zombie Pile co-op server: rooms of up to 3 players (quick match or a 5-letter private code).
// It does no game logic. The first player of a room is the HOST: their browser runs the whole game and
// streams snapshots; the others send their aim and buttons and draw what the host says.
//
// Lobby messages are JSON ({"t": ...}). Game messages are plain text frames that start with a letter
// (S, E, I, J): they are relayed without parsing, host -> everybody else, anybody else -> host.
"use strict";
const http = require("http");
const { WebSocketServer } = require("ws");

const PORT = process.env.PORT || 8080;
const PROTOCOL = 1;
const MAX_PLAYERS = 3;
const SLOT_ORDER = [1, 0, 2];        // host takes the wall (slot 1), then the left tower, then the right tower
const MAX_MSGS_PER_SEC = 240;
const CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

let nextId = 1;
const clients = new Set();
const rooms = new Map();             // code -> room

function send(ws, msg) { if (ws && ws.readyState === 1) ws.send(typeof msg === "string" ? msg : JSON.stringify(msg)); }
function newCode() {
  for (;;) {
    let c = "";
    for (let i = 0; i < 5; i++) c += CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)];
    if (!rooms.has(c)) return c;
  }
}
function cleanName(n) { return String(n || "").replace(/[^\w .\-]/g, "").slice(0, 14).toUpperCase() || "PLAYER"; }

function lobbyMsg(room, to) {
  return { t: "lobby", code: room.code, started: room.started, you: to.slot, host: room.players[0] === to,
           players: room.players.map(p => ({ slot: p.slot, name: p.name, host: p === room.players[0] })) };
}
function broadcastLobby(room) { for (const p of room.players) send(p, lobbyMsg(room, p)); }

function join(ws, room) {
  const used = new Set(room.players.map(p => p.slot));
  ws.slot = SLOT_ORDER.find(s => !used.has(s));
  room.players.push(ws);
  ws.room = room;
  broadcastLobby(room);
}

function leave(ws) {
  const room = ws.room;
  if (!room) return;
  ws.room = null;
  const wasHost = room.players[0] === ws;
  room.players = room.players.filter(p => p !== ws);
  if (wasHost || room.players.length === 0) {
    // the host's browser runs the game: without it the room is over
    for (const p of room.players) { p.room = null; send(p, { t: "closed" }); }
    rooms.delete(room.code);
  } else {
    for (const p of room.players) send(p, { t: "left", slot: ws.slot });
    broadcastLobby(room);
  }
}

const server = http.createServer((req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "*");
  if (req.url === "/stats") {
    res.setHeader("Content-Type", "application/json");
    let players = 0; for (const r of rooms.values()) players += r.players.length;
    res.end(JSON.stringify({ online: clients.size, rooms: rooms.size, playing: players }));
  } else { res.setHeader("Content-Type", "text/plain"); res.end("zombie pile server ok"); }
});

const wss = new WebSocketServer({ server, maxPayload: 64 * 1024 });
wss.on("connection", ws => {
  ws.id = nextId++; ws.alive = true; ws.room = null; ws.slot = 1; ws.name = "PLAYER"; ws.budget = MAX_MSGS_PER_SEC; ws.budgetT = Date.now();
  clients.add(ws);
  ws.on("pong", () => { ws.alive = true; });
  ws.on("message", data => {
    const now = Date.now();
    if (now - ws.budgetT > 1000) { ws.budgetT = now; ws.budget = MAX_MSGS_PER_SEC; }
    if (--ws.budget < 0) return;                                  // flood guard: drop
    const text = data.toString();
    const c = text.charCodeAt(0);
    if (c !== 123 /* { */) {                                     // game traffic: relay untouched
      const room = ws.room;
      if (!room || !room.started) return;
      if (room.players[0] === ws) { for (const p of room.players) if (p !== ws) send(p, text); }
      else send(room.players[0], text);
      return;
    }
    let m; try { m = JSON.parse(text); } catch (e) { return; }
    if (m.name) ws.name = cleanName(m.name);
    switch (m.t) {
      case "hello":
        if (m.v !== PROTOCOL) send(ws, { t: "error", code: "version", msg: "Please reload the game to play online." });
        else send(ws, { t: "hi", online: clients.size });
        break;
      case "quick": {
        leave(ws);
        // any open public room that has not started and has space, else a new one
        let room = null;
        for (const r of rooms.values()) if (!r.private && !r.started && r.players.length < MAX_PLAYERS) { room = r; break; }
        if (!room) { room = { code: newCode(), players: [], private: false, started: false }; rooms.set(room.code, room); }
        join(ws, room);
        break;
      }
      case "create": {
        leave(ws);
        const room = { code: newCode(), players: [], private: true, started: false };
        rooms.set(room.code, room);
        join(ws, room);
        break;
      }
      case "join": {
        const code = String(m.code || "").toUpperCase();
        const room = rooms.get(code);
        if (!room || room.started || room.players.length >= MAX_PLAYERS || room.players.includes(ws)) { send(ws, { t: "error", code: "noroom", msg: "That room is gone, full or already playing." }); break; }
        leave(ws);
        join(ws, room);
        break;
      }
      case "start": {
        const room = ws.room;
        if (!room || room.players[0] !== ws || room.started) break;
        room.started = true;
        for (const p of room.players) send(p, { t: "started" });
        broadcastLobby(room);
        break;
      }
      case "leave": leave(ws); break;
    }
  });
  ws.on("close", () => { leave(ws); clients.delete(ws); });
  ws.on("error", () => {});
});

setInterval(() => {
  for (const ws of clients) {
    if (!ws.alive) { ws.terminate(); continue; }
    ws.alive = false; try { ws.ping(); } catch (e) {}
  }
}, 15000);

server.listen(PORT, () => console.log("zombie pile server on :" + PORT));
module.exports = { server, wss };
