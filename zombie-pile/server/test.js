// Runs the server on a random port and plays a full co-op room with 3 clients.
"use strict";
process.env.PORT = 0;
const assert = require("assert");
const WebSocket = require("ws");
const { server } = require("./server.js");

function client(port, name) {
  return new Promise(res => {
    const ws = new WebSocket("ws://127.0.0.1:" + port);
    const c = { ws, name, inbox: [], waiters: [] };
    ws.on("message", d => {
      const t = d.toString();
      c.inbox.push(t);
      c.waiters = c.waiters.filter(w => { if (w.pred(t)) { w.res(t); return false; } return true; });
    });
    c.send = m => ws.send(typeof m === "string" ? m : JSON.stringify({ name, ...m }));
    c.wait = (pred, ms = 1500) => new Promise((resolve, reject) => {
      const hit = c.inbox.find(pred);
      if (hit) { c.inbox.splice(c.inbox.indexOf(hit), 1); return resolve(hit); }
      const w = { pred, res: t => { c.inbox.splice(c.inbox.indexOf(t), 1); resolve(t); } };
      c.waiters.push(w);
      setTimeout(() => reject(new Error(name + " timed out waiting")), ms);
    });
    c.json = (type, extra = {}) => c.wait(t => { try { const j = JSON.parse(t); return j.t === type && Object.entries(extra).every(([k, v]) => j[k] === v); } catch (e) { return false; } }).then(JSON.parse);
    ws.on("open", () => res(c));
  });
}

server.on("listening", async () => {
  const port = server.address().port;
  try {
    const a = await client(port, "ANA"), b = await client(port, "BOB"), c = await client(port, "CAM"), d = await client(port, "DAN");
    a.send({ t: "hello", v: 1 }); await a.json("hi");
    a.send({ t: "hello", v: 99 }); await a.json("error", { code: "version" });

    a.send({ t: "create" });
    let l = await a.json("lobby");
    assert.strictEqual(l.host, true); assert.strictEqual(l.you, 1); assert.strictEqual(l.code.length, 5);
    const code = l.code;

    b.send({ t: "join", code: "ZZZZZ" }); await b.json("error", { code: "noroom" });
    b.send({ t: "join", code: code.toLowerCase() });
    l = await b.json("lobby"); assert.strictEqual(l.you, 0); assert.strictEqual(l.host, false); assert.strictEqual(l.players.length, 2);
    await a.json("lobby");
    c.send({ t: "join", code });
    l = await c.json("lobby"); assert.strictEqual(l.you, 2); assert.strictEqual(l.players.length, 3);
    d.send({ t: "join", code }); await d.json("error", { code: "noroom" });      // full

    // game frames are ignored until the host starts
    a.send("S|early"); await new Promise(r => setTimeout(r, 100)); assert.ok(!b.inbox.some(t => t === "S|early"));

    b.send({ t: "start" }); await new Promise(r => setTimeout(r, 100)); assert.ok(!b.inbox.some(t => t.startsWith("{") && JSON.parse(t).t === "started"));   // only the host may start
    a.send({ t: "start" });
    await a.json("started"); await b.json("started"); await c.json("started");

    // host -> everyone else; guest -> host only
    a.send("S|1|snapshot"); assert.strictEqual(await b.wait(t => t.startsWith("S|")), "S|1|snapshot"); assert.strictEqual(await c.wait(t => t.startsWith("S|")), "S|1|snapshot");
    b.send("I|0|aim"); assert.strictEqual(await a.wait(t => t.startsWith("I|")), "I|0|aim");
    await new Promise(r => setTimeout(r, 100)); assert.ok(!c.inbox.some(t => t.startsWith("I|")), "guests must not see each other's input");

    // a started room can no longer be joined
    d.send({ t: "join", code }); await d.json("error", { code: "noroom" });

    // a guest leaves: the others hear it, the slot frees up
    c.ws.close();
    const left = await a.json("left"); assert.strictEqual(left.slot, 2);
    l = JSON.parse(await a.wait(t => { try { const j = JSON.parse(t); return j.t === "lobby" && j.players.length === 2; } catch (e) { return false; } })); assert.strictEqual(l.players.length, 2);

    // the host leaves: the room closes
    a.ws.close(); await b.json("closed");

    // quick match: two strangers land in the same room
    const e = await client(port, "EVE"), f = await client(port, "FAY");
    e.send({ t: "quick" }); const le = await e.json("lobby");
    f.send({ t: "quick" }); const lf = await f.json("lobby");
    assert.strictEqual(le.code, lf.code); assert.strictEqual(lf.players.length, 2);

    // stats endpoint
    const http = require("http");
    const stats = await new Promise(r => http.get("http://127.0.0.1:" + port + "/stats", res => { let s = ""; res.on("data", x => s += x); res.on("end", () => r(JSON.parse(s))); }));
    assert.ok(stats.rooms >= 1);
    console.log("server tests passed");
    process.exit(0);
  } catch (err) { console.error("FAILED:", err.message); process.exit(1); }
});
