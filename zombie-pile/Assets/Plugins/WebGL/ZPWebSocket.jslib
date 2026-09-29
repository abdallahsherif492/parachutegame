// A minimal WebSocket bridge for the WebGL build (Unity WebGL has no sockets of its own).
// C# side: Assets/Scripts/Net/Transport.cs. Text frames only; incoming frames are queued and popped one by one.
var ZPWebSocket = {
  $zp: { ws: null, state: 0, q: [] },

  // state: 0 idle, 1 connecting, 2 open, 3 closed after being open, 4 failed
  ZP_WS_Open: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    try { if (zp.ws) { var old = zp.ws; zp.ws = null; old.close(); } } catch (e) {}
    zp.q = []; zp.state = 1;
    try {
      var ws = new WebSocket(url);
      zp.ws = ws;
      ws.onopen = function () { if (zp.ws === ws) zp.state = 2; };
      ws.onmessage = function (e) { if (zp.ws === ws && typeof e.data === "string") zp.q.push(e.data); };
      ws.onclose = function () { if (zp.ws === ws) zp.state = (zp.state === 2 ? 3 : 4); };
      ws.onerror = function () { if (zp.ws === ws && zp.state === 1) zp.state = 4; };
    } catch (e) { zp.state = 4; }
  },

  ZP_WS_State: function () { return zp.state; },

  ZP_WS_Send: function (msgPtr) {
    if (zp.ws && zp.state === 2) { try { zp.ws.send(UTF8ToString(msgPtr)); } catch (e) {} }
  },

  ZP_WS_Pending: function () { return zp.q.length; },

  // returns a malloc'ed UTF-8 string that C# frees with ZP_WS_Free
  ZP_WS_Pop: function () {
    var s = zp.q.length ? zp.q.shift() : "";
    var n = lengthBytesUTF8(s) + 1;
    var p = _malloc(n);
    stringToUTF8(s, p, n);
    return p;
  },

  ZP_WS_Free: function (p) { _free(p); },

  ZP_WS_Close: function () {
    try { if (zp.ws) { var old = zp.ws; zp.ws = null; zp.state = 0; old.close(); } } catch (e) {}
    zp.q = [];
  }
};
autoAddDeps(ZPWebSocket, "$zp");
mergeInto(LibraryManager.library, ZPWebSocket);
