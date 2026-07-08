# Codex Guardian

Codex Guardian は、Windows の通知領域に常駐して Codex プロセスを見守る小さなツールです。

## 主な機能

- Codex プロセスの終了検出と自動再起動
- Windows 起動時の自動開始
- 通知領域アイコンと右クリックメニュー
- 設定画面からの Codex パス、起動引数、再起動間隔の変更
- 任意で有効にできる Codex Guardian 自身の自動復旧

## 初期設定

- Codex の守護: 有効
- Windows 起動時の開始: 有効
- Codex Guardian 自身の自動復旧: 無効
- Codex の起動コマンド: `codex`
- 再起動までの待ち時間: 5 秒

## 発行

次のコマンドで、.NET ランタイムを別途入れなくても動作する単一ファイル版を作成できます。

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

出力先は `bin\Release\net9.0-windows\win-x64\publish` です。

## GitHub Release

`main` ブランチに push すると、GitHub Actions が `v0.1.0` の Release を作成し、次のファイルを添付します。

- `CodexGuardian.exe`
- `CodexGuardian-win-x64.zip`
