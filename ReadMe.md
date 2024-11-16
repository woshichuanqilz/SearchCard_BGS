# Demo

![](https://raw.githubusercontent.com/woshichuanqilz/MyImage/master/Demo.gif)

# Purpose

This is a plugin for Hearthstone Battlegrounds, it can search cards by

1. Tags
2. Keywords
3. Mechanics
4. Races

- Type in cardId/DbfId in the inputbox.
- Or type in the feature(battlecry, deathrattle, etc.) of the card in the combobox.
- Click on the tag at the result list use tag to filter the result.
- Search cardId/DbfId in the result list.
- Hover on the Image to see the larger image of the card.

You can check thie right side panel of minion information on this [wiki.gg for Accord-o-Tron](https://hearthstone.wiki.gg/wiki/Battlegrounds/Accord-o-Tron) to know the details of the tags, keywords, mechanics and races.

Mainly information of the minion cards are from [HearthSim/hsdata](https://github.com/HearthSim/hsdata), and [wiki.gg](https://hearthstone.wiki.gg/).

# Configuration

If you want to change the locale of the search result, you can change the `_locale` in `SearchWindow.xaml.cs`.

- `Locale.enUS` for English
- `Locale.zhCN` for Chinese
- ...

# Notice

I use Proxy to download the data. If you occur warning of "Download data error", please check the proxy code in this `MyPlugin.cs`.

```csharp
var handler = new HttpClientHandler()
{
    Proxy = new WebProxy("http://127.0.0.1:10081"),
    UseProxy = true // set to false if you want to use the default system proxy
};
```
