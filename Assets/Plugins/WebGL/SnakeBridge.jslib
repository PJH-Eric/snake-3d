// WebGL 與瀏覽器溝通的橋：WebSocket（以輪詢事件佇列的方式，不依賴函式指標回呼）、網址、剪貼簿、分享、震動。
var SnakeBridge = {
  $SnakeWs: { list: [], next: 1 },

  SnakeWsOpen: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = SnakeWs.next++;
    var rec = { ws: null, q: [], state: 0 };
    SnakeWs.list[id] = rec;
    try {
      var ws = new WebSocket(url);
      rec.ws = ws;
      ws.onopen = function () { rec.state = 1; rec.q.push('O'); };
      ws.onmessage = function (e) { if (typeof e.data === 'string') rec.q.push('M' + e.data); };
      ws.onerror = function () { rec.q.push('E'); };
      ws.onclose = function () { rec.state = 3; rec.q.push('C'); };
    } catch (err) { rec.state = 3; rec.q.push('E'); rec.q.push('C'); }
    return id;
  },

  SnakeWsState: function (id) { var r = SnakeWs.list[id]; return r ? r.state : 3; },

  SnakeWsSend: function (id, msgPtr) {
    var r = SnakeWs.list[id];
    if (r && r.ws && r.ws.readyState === 1) { r.ws.send(UTF8ToString(msgPtr)); return 1; }
    return 0;
  },

  SnakeWsClose: function (id) {
    var r = SnakeWs.list[id];
    if (r && r.ws) { try { r.ws.onclose = null; r.ws.close(); } catch (e) {} r.state = 3; }
    delete SnakeWs.list[id];
  },

  // 取出下一筆事件寫進 C# 的緩衝區。回傳寫入的位元組數；0 = 沒有事件；負數 = 緩衝區太小（絕對值是需要的大小，事件保留）
  SnakeWsPoll: function (id, bufPtr, cap) {
    var r = SnakeWs.list[id];
    if (!r || r.q.length === 0) return 0;
    var s = r.q[0];
    var need = lengthBytesUTF8(s);
    if (need + 1 > cap) return -(need + 1);
    r.q.shift();
    stringToUTF8(s, bufPtr, cap);
    return need;
  },

  SnakeGetHref: function () {
    var s = (typeof location !== 'undefined') ? location.href : '';
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },

  SnakeReplaceUrl: function (pathPtr) {
    try { history.replaceState(history.state, '', UTF8ToString(pathPtr)); } catch (e) {}
  },

  SnakeCopyText: function (textPtr) {
    var t = UTF8ToString(textPtr);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(t); return 1; }
    } catch (e) {}
    try {
      var ta = document.createElement('textarea'); ta.value = t; ta.style.position = 'fixed'; ta.style.opacity = '0';
      document.body.appendChild(ta); ta.select(); var ok = document.execCommand('copy'); document.body.removeChild(ta); return ok ? 1 : 0;
    } catch (e2) { return 0; }
  },

  SnakeCanShare: function () { return (typeof navigator !== 'undefined' && navigator.share) ? 1 : 0; },

  SnakeShare: function (titlePtr, textPtr, urlPtr) {
    try { navigator.share({ title: UTF8ToString(titlePtr), text: UTF8ToString(textPtr), url: UTF8ToString(urlPtr) }).catch(function () {}); } catch (e) {}
  },

  SnakeVibrate: function (ms) { try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) {} },

  SnakeSetPage: function (langPtr, titlePtr) {
    try { document.documentElement.lang = UTF8ToString(langPtr); document.title = UTF8ToString(titlePtr); } catch (e) {}
  },

  SnakeCanvasCssWidth: function () {
    return Math.max(1, Math.round(Module.canvas.getBoundingClientRect().width));
  },

  SnakeIsTouchDevice: function () {
    try { return (('ontouchstart' in window) || navigator.maxTouchPoints > 0) && window.matchMedia && window.matchMedia('(pointer: coarse)').matches ? 1 : 0; } catch (e) { return 0; }
  }
};

autoAddDeps(SnakeBridge, '$SnakeWs');
mergeInto(LibraryManager.library, SnakeBridge);
