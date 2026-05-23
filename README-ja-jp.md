| [English](README.md) | 日本語 |

# まばたきサプレッサー
VRChat アバターのための非破壊まばたき抑制ツールです。

`VRC Avatar Descriptor` で定義されたまばたきを、視線追従機能を維持したまま任意のタイミングで抑制することができます。  
非破壊ツールなのでコンポーネントを削除するだけでいつでも元に戻すことができます。

## インストール
1. [VPM Listing](https://vpm.nekobako.net) の `Add to VCC` ボタンを押してリポジトリを追加します。
2. プロジェクトの `Manage Project` ボタンを押します。
3. `Blink Suppressor` パッケージの右にある `+` ボタンを押します。

## 使い方
1. `Blink Suppressor` コンポーネントをアバターに追加します (アバター内のどこに置いても構いません)。

![Inspector](https://github.com/user-attachments/assets/0f301726-2c63-42b1-a045-d873b8bd73b3)

![Inspector](https://github.com/user-attachments/assets/65a91b1b-5f80-4ff3-ba3f-ab1e3e113768)

2. まばたきを抑制したいときのみ `まばたきを抑制` プロパティーが `true` になり、そうでないときは `false` になるようにアニメーションを組みます。

![Animation](https://github.com/user-attachments/assets/27d0acb9-76bf-4bbe-9fa3-0c6acdd176c1)

![Animation](https://github.com/user-attachments/assets/1bca1680-09f7-4d65-a333-f416775ecb8c)
