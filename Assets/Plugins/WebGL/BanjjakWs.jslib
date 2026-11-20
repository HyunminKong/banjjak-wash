// Browser WebSocket bridge for the co-op mode. C# polls: messages are queued here and pulled one per call.
mergeInto(LibraryManager.library, {
  BjWsOpen: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (!window.bjws) window.bjws = { next: 1, socks: {} };
    var id = window.bjws.next++;
    var s = { ws: null, q: [], state: 0, err: "" };
    window.bjws.socks[id] = s;
    try {
      var ws = new WebSocket(url);
      s.ws = ws;
      ws.onopen = function () { s.state = 1; };
      ws.onmessage = function (e) { if (typeof e.data === "string") s.q.push(e.data); };
      ws.onerror = function () { s.err = "error"; };
      ws.onclose = function (e) { s.state = 3; if (!s.err) s.err = "closed " + e.code; };
    } catch (e) { s.state = 3; s.err = "" + e; }
    return id;
  },

  // 0 connecting, 1 open, 3 closed
  BjWsState: function (id) {
    var s = window.bjws && window.bjws.socks[id];
    return s ? s.state : 3;
  },

  BjWsSend: function (id, strPtr) {
    var s = window.bjws && window.bjws.socks[id];
    if (!s || s.state !== 1) return 0;
    try { s.ws.send(UTF8ToString(strPtr)); return 1; } catch (e) { return 0; }
  },

  BjWsRecv: function (id) {
    var s = window.bjws && window.bjws.socks[id];
    if (!s || s.q.length === 0) return 0;
    var str = s.q.shift();
    var size = lengthBytesUTF8(str) + 1;
    var buf = _malloc(size);
    stringToUTF8(str, buf, size);
    return buf;
  },

  BjWsClose: function (id) {
    var s = window.bjws && window.bjws.socks[id];
    if (!s) return;
    try { if (s.ws) s.ws.close(); } catch (e) {}
    s.state = 3;
    delete window.bjws.socks[id];
  },

  // ?room=1234 in the page address, for invite links
  BjPageQuery: function () {
    var str = window.location.search || "";
    var size = lengthBytesUTF8(str) + 1;
    var buf = _malloc(size);
    stringToUTF8(str, buf, size);
    return buf;
  }
});
