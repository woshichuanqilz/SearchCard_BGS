﻿using CardSearcher.Controls;
using CardSearcher.Logic;
using CardSearcher.Properties;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Utility;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;
using Core = Hearthstone_Deck_Tracker.API.Core;
using System.Windows.Media.Imaging;
using System.IO;
using System.Net;
using System.Security.Policy;
using HearthDb;
using HearthDb.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Net.Http;
using Newtonsoft.Json; // 确保你已经安装了 Newtonsoft.Json 包
using System.Threading.Tasks;

namespace CardSearcher
{
    /// <summary>
    /// This is where we put the logic for our Plug-in
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class CardSearcher : IDisposable
    {
        // ToDo: The window shouldn't be statically named
        private static string panelName = "pluginStackPanelView";

        /// <summary>
        /// The class that allows us to let the user move the panel
        /// </summary>
        public static InputMoveManager inputMoveManager;

        /// <summary>
        /// The panel reference we will display our plug-in magic within
        /// </summary>
        public PlugInDisplayControl stackPanel;

        private IEnumerable<HearthDb.Card> baconCards; // 添加成员变量

        private List<CardWikiData> cardDataList; // 添加成员变量

        /// <summary>
        /// Initializes a new instance of the <see cref="CardSearcher"/> class.
        /// </summary>
        public CardSearcher()
        {
            var overrides = new Dictionary<int, Tuple<GameTag, int>>();
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));

            int GetTag(HearthDb.Card card, GameTag tag)
            {
                if (overrides.TryGetValue(card.DbfId, out var tagOverride) && tagOverride.Item1 == tag) return tagOverride.Item2;
                return card.Entity.GetTag(tag);
            }

            baconCards = Cards.All.Values
                .Where(x =>
                    GetTag(x, GameTag.TECH_LEVEL) > 0
                    && GetTag(x, GameTag.IS_BACON_POOL_MINION) > 0
                    && GetTag(x, GameTag.IS_BACON_POOL_MINION) > 0
                ).ToList(); // 将结果存储到成员变量中
        }

        public void CleanUp()
        {
            if (stackPanel == null) return;
            Core.OverlayCanvas.Children.Remove(stackPanel);
            Dispose();
        }

        public void Dispose()
        {
            inputMoveManager.Dispose();
        }


        public BitmapImage GetCardImage(string cardId)
        {
            // 如果image文件夹不存在，则创建
            if (!Directory.Exists("image"))
            {
                Directory.CreateDirectory("image");
            }

            var imagePath = Path.Combine("image", $"{cardId}.jpg");

            // 检查文件是否存在
            if (File.Exists(imagePath))
            {
                return new BitmapImage(new Uri(imagePath, UriKind.Relative));
            }
            else
            {
                // 下载图片并保存
                var url = $"https://static.zerotoheroes.com/hearthstone/cardart/256x/{cardId}.jpg";
                using (var client = new WebClient())
                {
                    client.DownloadFile(url, imagePath);
                }
                return new BitmapImage(new Uri(imagePath, UriKind.Relative));
            }
        }

        // 函数来获取可用种族
        public List<Race> GetAvailableRaces()
        {
            return Core.Game.CurrentGameStats?.GameId != null ? (BattlegroundsUtils.GetAvailableRaces(Core.Game.CurrentGameStats?.GameId) ?? new HashSet<Race>()).ToList() : new List<Race>();
        }

        public IEnumerable<HearthDb.Card> GetBaconCards() // 提供访问方法
        {
            return baconCards;
        }

        [STAThread]
        static void Main()
        {
            var app = new Application();

            // 创建并显示 SearchWindow
            var sw = new SearchWindow();
            sw.Show();
            app.Run(); // 启动应用程序
        }

        public async Task<List<CardWikiData>> DownloadAndParseJsonAsync()
        {
            const string url =
                "https://hearthstone.wiki.gg/wiki/Special:CargoExport?tables=Card,%20CardTag,%20DerivedCard,%20CustomCard,%20CardTagBg&join%20on=Card.dbfId=CardTag.dbfId,%20CardTag.dbfId=DerivedCard.dbfId,%20DerivedCard.dbfId=CustomCard.dbfId,%20CustomCard.dbfId=CardTagBg.dbfId&fields=CONCAT(Card.dbfId)=dbfId,%20Card.id=id,%20CONCAT(Card.name)=name,%20DerivedCard.minionTypeStrings=Races,%20CardTag.keywords=keywords,%20CardTag.refs=refs,%20CardTag.stringTags=stringTags,%20CONCAT(CustomCard.mechanicTags__full)=wikiMechanics,%20CONCAT(CustomCard.refTags__full)=wikiTags,%20CONCAT(CustomCard.hiddenTags__full)=wikiHiddenTags&where=CardTagBg.isPoolMinion=1&limit=2000&format=json";

            var handler = new HttpClientHandler()
            {
                Proxy = new WebProxy("http://127.0.0.1:10081"),
                UseProxy = true
            };

            using (var client = new HttpClient(handler))
            {
                // 下载 JSON 数据
                try
                {
                    var json = await client.GetStringAsync(url);
                    // 解析 JSON 数据并赋值给成员变量
                    cardDataList = JsonConvert.DeserializeObject<List<CardWikiData>>(json);

                    foreach (var card in cardDataList)
                    {
                        if (!string.IsNullOrEmpty(card.StringTags))
                        {
                            card.TagsList = card.StringTags.Split(' ')
                                .Select(tag => tag.Split('=')[0]) // 取每个 tag 的左侧部分
                                .Distinct() // 去重
                                .ToList(); // 转换为 List<string>
                        }

                        // 处理 wikiMechanics 字段
                        card.WikiMechanicsList = CleanWikiTags(card.WikiMechanics);
                        // 处理 wikiTags 字段 should split by "&amp;&amp;"
                        card.WikiTagsList = card.WikiTags?.Split(new[] { "&amp;&amp;" }, StringSplitOptions.None).ToList();
                        // 处理 keywords 字段 and make it lower case
                        card.KeywordsList = card.Keywords?.ToLower().Split(' ').ToList(); // 将 keywords 转换为 List<string>
                        // 处理 Races 字段 should split by "&amp;&amp;"
                        card.RacesList = card.Races[0]?.Split(new[] { "&amp;&amp;" }, StringSplitOptions.None).ToList();
                        // 如果 RacesList 的第一个元素为空，则设置为 "Neutral"
                        if (card.RacesList != null && card.RacesList.FirstOrDefault() == "")
                        {
                            card.RacesList = new List<string> { "Neutral" };
                        }
                    }

                    return cardDataList; // 返回包含关键字的 CardWikiData 列表
                }
                catch (HttpRequestException e)
                {
                    // 处理请求异常
                    Console.WriteLine($"请求失败: {e.Message}");
                }
                catch (Exception e)
                {
                    // 处理其他异常
                    Console.WriteLine($"发生错误: {e.Message}");
                }
            }

            return new List<CardWikiData>(); // 返回空列表
        }

        // GetCardsByTagsAndRaces
        public Task<List<CardWikiData>> GetCardsByTagsAndRaces(List<string> tags, List<string> races)
        {
            // 根据 tags 和 races 进行搜索 需要搜索包含所有 tags 和 races 的卡片, wikiTagsList 和 RacesList 都是 List<string> and not null 
            var resultList = cardDataList.Where(card =>
                (card.WikiTagsList != null && tags.All(tag => card.WikiTagsList.Contains(tag))) &&
                (card.RacesList != null && races.All(race => (card.RacesList.Contains(race) || card.RacesList.Contains("All"))))
            ).ToList();
            return Task.FromResult(resultList);
        }

        private static List<string> CleanWikiTags(string tags)
        {
            if (string.IsNullOrEmpty(tags))
            {
                return new List<string>(); // 返回空列表
            }

            // 根据空格分割字符串并返回 List<string>
            var cleanedTags = tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return cleanedTags.Select(tag => tag.Trim()).ToList(); // 返回清理后的 List<string>
        }

        public class CardWikiData
        {
            public string DbfId { get; set; }
            public string Id { get; set; }
            public string Name { get; set; }
            public string Keywords { get; set; } // 原始 keywords 字段
            public List<string> KeywordsList { get; set; } // 新增 KeywordsList 属性
            public string StringTags { get; set; }
            public List<string> TagsList { get; set; } // 新增 TagsList 属性
            public string WikiMechanics { get; set; } // wikiMechanics 属性
            public List<string> WikiMechanicsList { get; set; } // 新增 wikiMechanicsList 属性
            public string WikiTags { get; set; } // wikiTags 属性
            public List<string> WikiTagsList { get; set; } // 新增 wikiTagsList 属性
            public string WikiHiddenTags { get; set; } // 原始 wikiHiddenTags 属性
            public List<string> WikiHiddenTagsList { get; set; } // 新增 wikiHiddenTagsList 属性
            public List<string> Races { get; set; } // 新增 Races 属性
            public List<string> RacesList { get; set; } // 新增 Races 属性
        }

        public List<CardWikiData> CardDataList { get; private set; } // 公共属性，供其他文件访问

        public async Task Run()
        {
            CardDataList = await DownloadAndParseJsonAsync(); // 确保在实例方法中调用
            Console.WriteLine("wiki data done");
        }
    }

}