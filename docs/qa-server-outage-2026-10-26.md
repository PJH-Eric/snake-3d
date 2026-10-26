# 線上伺服器暫停提示

- 線上連線持續 30 秒未成功時，以既有動物園風格的自訂 Unity Modal 顯示次月 1 日額度重置及可繼續單機的簡短說明。
- 沿用繁中／英文、既有按鈕、關閉與捲動行為；同次故障只提醒一次，連線恢復或退出線上時取消。
- 原始字典新增於 Backend~/public/js/locales.js，LocData.cs 由 Tools/genloc.js 重新產生。已確認 UIFont.otf 包含新文案的所有中文字。

## 已驗證

- `dotnet run --project GameCheck~/InputTests/InputTests.csproj`：通過。包含 30 秒邊界、重試不延後提示、故障去重、退出取消、恢復、重新進入與全年 12 個月份／跨年計算。
- `dotnet build GameCheck~/GameCheck.csproj -p:DefineConstants=UNITY_WEBGL`：成功，沒有編譯錯誤；4 個既有未使用欄位警告。
- `npm --prefix Backend~ test`：全部通過，涵蓋規則、AI、房間、邀請、觀戰、WebSocket 及 WebGL 橋接。
- `dotnet run --project CoreTests~/CoreTests.csproj -- <golden.json>`：20 項通過，使用本次生成的 JavaScript golden 資料。測試原有的 AI 浮點分岔容許規則不變；未提供 snap.json 的線上快照比對依原測試略過。
- Git diff 空白檢查與 UTF-8 無 BOM：通過。

## 限制

- 此環境未找到可執行的 Unity Editor，沒有執行 Play Mode、真正的 WebGL 建置或手機／平板／桌機遊戲畫面驗收。C# 替身編譯不能視為 Unity 建置或畫面通過。
- 沒有刻意耗盡 Render 配額；提示以持續連線失敗為觸發，額度重置日期不代表已查到帳戶配額。
- Repository 沒有設定 Git remote；可建立本機 commit，無法完成 push。

## 提示僅限大廳／房間（同日追加）

- App.Go 依 LobbyPage／RoomPage 啟用提示；GamePage、首頁及其他畫面停用。進入遊戲會取消計時並關閉已有提示。
- 畫面狀態與網路狀態分開追蹤。遊戲中重連不提示，返回未連線房間才重新等待 30 秒。
- InputTests 新增首頁背景連線不提示、遊戲中重連不提示、進入遊戲清除已顯示／等待中提示，以及回房重新等待的案例，全部通過。
- 一般與 UNITY_WEBGL 替身編譯通過；仍未執行 Unity Editor／真正 WebGL 建置及畫面驗收。
