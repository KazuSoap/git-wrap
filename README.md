# git-wrap

"git-wrap" is for using MSYS2 git on VSCode.

## Setup

`git pull` / `fetch` / `push` を正しく動作させるには、VSCode 側の設定
（`terminal.integrated.env.windows` 等）で以下の環境変数を設定してください。

```json
"terminal.integrated.env.windows": {
  "MSYSTEM": "MSYS",
  "CHERE_INVOKING": "1"
}
```

## Reference

* [[FYI] Using git on msys2  #4651](https://github.com/Microsoft/vscode/issues/4651)
* [MSYS2 git causes Error: spawn C:/msys64/usr/bin/git.exe ENOENT due to path format  #965](https://github.com/gitkraken/vscode-gitlens/issues/965)
