// Bridge to the CrazyGames HTML5 SDK v3 (https://docs.crazygames.com/sdk/html5-v3/) for the WebGL build.
// The SDK script is loaded from here, so no Unity package is needed. C# side: Assets/Scripts/Core/PlatformSDK.cs
// (it polls; nothing here calls back into C#).
var ZPCrazy = {
  $zpc: {
    state: 0,        // 0 not started, 1 loading, 2 ready, 3 not available (other website, blocked, no network)
    sdk: null,
    ev: [],          // ad events waiting for C#: 1 started, 2 finished, 3 error
    ad: false,       // an ad is running

    str: function (s) {
      s = s || "";
      var n = lengthBytesUTF8(s) + 1;
      var p = _malloc(n);
      stringToUTF8(s, p, n);
      return p;
    }
  },

  ZP_Sdk_Init: function () {
    if (zpc.state !== 0) return;
    zpc.state = 1;
    var fail = function () { zpc.state = 3; };
    try {
      if (window.CrazyGames && window.CrazyGames.SDK) { zpc.sdk = window.CrazyGames.SDK; }
      var start = function () {
        var sdk = window.CrazyGames && window.CrazyGames.SDK;
        if (!sdk) { fail(); return; }
        Promise.resolve(sdk.init()).then(function () {
          zpc.sdk = sdk;
          zpc.state = (sdk.environment === "disabled") ? 3 : 2;
        }).catch(fail);
      };
      if (zpc.sdk) { start(); return; }
      var s = document.createElement("script");
      s.src = "https://sdk.crazygames.com/crazygames-sdk-v3.js";
      s.onload = start;
      s.onerror = fail;
      document.head.appendChild(s);
    } catch (e) { fail(); }
  },

  ZP_Sdk_State: function () { return zpc.state; },

  // game.<name>(): gameplayStart, gameplayStop, happytime, loadingStart, loadingStop
  ZP_Sdk_Game: function (namePtr) {
    if (zpc.state !== 2) return;
    try { zpc.sdk.game[UTF8ToString(namePtr)](); } catch (e) {}
  },

  // type: "midgame" or "rewarded"; progress is reported through ZP_Sdk_AdEvent
  ZP_Sdk_RequestAd: function (typePtr) {
    if (zpc.state !== 2 || zpc.ad) { zpc.ev.push(3); return; }
    zpc.ad = true;
    try {
      zpc.sdk.ad.requestAd(UTF8ToString(typePtr), {
        adStarted: function () { zpc.ev.push(1); },
        adFinished: function () { zpc.ad = false; zpc.ev.push(2); },
        adError: function () { zpc.ad = false; zpc.ev.push(3); }
      });
    } catch (e) { zpc.ad = false; zpc.ev.push(3); }
  },

  // next ad event (1 started, 2 finished, 3 error) or 0 when there is none
  ZP_Sdk_AdEvent: function () { return zpc.ev.length ? zpc.ev.shift() : 0; },

  // saved game in the player's CrazyGames account (synced between devices); "" when there is none
  ZP_Sdk_GetItem: function (keyPtr) {
    if (zpc.state !== 2) return zpc.str("");
    try { return zpc.str(zpc.sdk.data.getItem(UTF8ToString(keyPtr)) || ""); } catch (e) { return zpc.str(""); }
  },

  ZP_Sdk_SetItem: function (keyPtr, valPtr) {
    if (zpc.state !== 2) return;
    try { zpc.sdk.data.setItem(UTF8ToString(keyPtr), UTF8ToString(valPtr)); } catch (e) {}
  },

  // co-op invite: shows CrazyGames' invite button for this room code / the room code from the link the player opened
  ZP_Sdk_ShowInvite: function (codePtr) {
    if (zpc.state !== 2) return;
    try { zpc.sdk.game.showInviteButton({ room: UTF8ToString(codePtr) }); } catch (e) {}
  },

  ZP_Sdk_HideInvite: function () {
    if (zpc.state !== 2) return;
    try { zpc.sdk.game.hideInviteButton(); } catch (e) {}
  },

  ZP_Sdk_InviteParam: function () {
    if (zpc.state !== 2) return zpc.str("");
    try { return zpc.str(zpc.sdk.game.getInviteParam("room") || ""); } catch (e) { return zpc.str(""); }
  },

  ZP_Sdk_Free: function (p) { _free(p); }
};
autoAddDeps(ZPCrazy, "$zpc");
mergeInto(LibraryManager.library, ZPCrazy);
