| [English](README.md) | 日本語 |

# まばたきサプレッサー
VRChat アバターのための非破壊まばたき抑制ツールです。

VRC Avatar Descriptor で定義されたまばたきを、視線追従の機能を維持したまま任意のタイミングで抑制することができます。  
非破壊ツールなのでコンポーネントを削除するだけでいつでも元に戻すことができます。

> [!CAUTION]
> 現在、このツールを使用したアバターを特定のワールドで着用すると VRChat がクラッシュする不具合があります。  
> 問題が解消されるまで、このツールの使用は控えてください。  
> https://feedback.vrchat.com/bug-reports/p/1680-vrchat-crash-when-using-avatars-with-extremely-large-blendshape-position-de

## インストール
1. [VPM Listing](https://vpm.nekobako.net) の `Add to VCC` ボタンを押してリポジトリを追加します。
2. プロジェクトの `Manage Project` ボタンを押します。
3. `Blink Suppressor` パッケージの右にある `+` ボタンを押します。

## 使い方
1. `Blink Suppressor` コンポーネントをアバターに追加します (アバター内のどこに置いても構いません)。

![Inspector](https://github.com/user-attachments/assets/0f301726-2c63-42b1-a045-d873b8bd73b3)

![Inspector](https://github.com/user-attachments/assets/28a15030-b588-4022-9cf4-4cc403b85def)

2. 必要に応じて `Suppress Blink` プロパティーをアニメーションします。

![Animation](https://github.com/user-attachments/assets/27d0acb9-76bf-4bbe-9fa3-0c6acdd176c1)

![Animation](https://github.com/user-attachments/assets/1bca1680-09f7-4d65-a333-f416775ecb8c)
