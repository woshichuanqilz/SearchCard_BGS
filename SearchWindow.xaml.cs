using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HearthDb;
using HearthDb.CardDefs;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Hearthstone.EffectSystem.Effects.Mage;

namespace CardSearcher
{
    public class SearchItem : INotifyPropertyChanged
    {
        private string _name;
        private SolidColorBrush _bkgColor;
        private SolidColorBrush _foreColor;
        public string ItemType { get; set; }
        private Locale _locale;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public SolidColorBrush ForeColor
        {
            get => _foreColor;
            set
            {
                _foreColor = value;
                OnPropertyChanged(nameof(ForeColor));
            }
        }

        public SolidColorBrush BkgColor
        {
            get => _bkgColor;
            set
            {
                _bkgColor = value;
                OnPropertyChanged(nameof(BkgColor));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// SearchWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SearchWindow
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private readonly ObservableCollection<CardResult> _cardResults;
        private readonly ObservableCollection<SearchItem> _searchFilterItems;
        private readonly CardSearcher _cardSearcher;
        private Dictionary<string, List<SolidColorBrush>> colorDictionary = new Dictionary<string, List<SolidColorBrush>>();
        private bool _isSelectingFromDropBox = false; // 添加标志变量
        private Locale _locale;

        public class TagContent
        {
            public string Name { get; set; }
            public string ItemType { get; set; }
            public SolidColorBrush ForeColor { get; set; }
            public SolidColorBrush BkgColor { get; set; }
        }

        // 用于存储卡片结果的类
        public class CardResult
        {
            public BitmapImage ImageSource { get; set; }
            public string DisplayText { get; set; }

            // 用于存储标签的列表
            public List<TagContent> Tags1 { get; set; }
            public List<TagContent> Tags2 { get; set; }
        }

        public SearchWindow()
        {
            InitializeComponent();
            InputBox.TextChanged += InputBox_TextChanged;
            _cardResults = new ObservableCollection<CardResult>();
            ResultsListView.ItemsSource = _cardResults;
            _searchFilterItems = new ObservableCollection<SearchItem>();
            SearchFilter.ItemsSource = _searchFilterItems;

            // 初始化 _locale
            _locale = Locale.enUS; // 或者根据需要设置为其他值

            // 在 DownloadAndParseJsonAsync 方法中
            colorDictionary["WikiMechanics"] = new List<SolidColorBrush>
            {
                Brushes.White,
                new SolidColorBrush(Color.FromRgb(133, 20, 75))
            };

            colorDictionary["WikiTags"] = new List<SolidColorBrush>
            {
                Brushes.Black,
                Brushes.Orange,
                // 可以添加更多颜色
            };

            colorDictionary["Keywords"] = new List<SolidColorBrush>
            {
                Brushes.Black,
                Brushes.Yellow
            };

            colorDictionary["Races"] = new List<SolidColorBrush>
            {
                Brushes.White,
                Brushes.Blue
                // 可以添加更多颜色
            };

            // 订阅 CollectionChanged 事件
            _searchFilterItems.CollectionChanged += SearchFilterItems_CollectionChanged;

            _cardSearcher = new CardSearcher();
            Task.Run(async () => await _cardSearcher.Run()); // 使用 Task.Run 来处理异步调用
        }

        // async method
        private async void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tmpId = InputBox.Text;
            if (tmpId.Length < 3) return;
            // 获取匹配的卡片
            var resultList = new List<Card>();
            var isBg = tmpId.ToLower().StartsWith("bg");
            var isNumber = tmpId.All(char.IsDigit);
            // tmp_Id is start with "bg"
            if (isBg)
            {
                resultList = await Task.Run(
                    () =>
                        _cardSearcher
                            .GetBaconCards()
                            .Where(kvp => kvp.Id.ToLower().Contains(tmpId.ToLower()))
                            .ToList()
                );
            }
            // tmp_Id is number
            else if (isNumber)
            {
                resultList = await Task.Run(
                    () =>
                        _cardSearcher
                            .GetBaconCards()
                            .Where(kvp => kvp.DbfId.ToString().Contains(tmpId))
                            .ToList()
                );
            }

            var cardDataList = _cardSearcher.CardDataList;

            // 清空之前的结果
            _cardResults.Clear();

            // 遍历结果并下载图片
            foreach (var tmpCard in resultList)
            {
                var image = await GetCardImageAsync(tmpCard.Id); // 获取卡片图片
                var keywords = new List<TagContent>();
                var races = new List<TagContent>();
                var wikiTags = new List<TagContent>();
                var wikiMechanics = new List<TagContent>();
                if (isBg)
                {
                    // wikitags if wikiTagsList is null, create a new list
                    wikiTags = cardDataList.Find(card => card.Id == tmpCard.Id).WikiTagsList?.Select(tag => new TagContent { Name = tag, ItemType = "WikiTags" }).ToList() ?? new List<TagContent>();
                    keywords = cardDataList.Find(card => card.Id == tmpCard.Id).KeywordsList?.Select(tag => new TagContent { Name = tag, ItemType = "Keywords" }).ToList() ?? new List<TagContent>();
                    races = cardDataList.Find(card => card.Id == tmpCard.Id).RacesList?.Select(tag => new TagContent { Name = tag, ItemType = "Races" }).ToList() ?? new List<TagContent>();
                    wikiMechanics = cardDataList.Find(card => card.Id == tmpCard.Id).WikiMechanicsList?.Select(tag => new TagContent { Name = tag, ItemType = "WikiMechanics" }).ToList() ?? new List<TagContent>();
                }
                else if (isNumber)
                {
                    wikiTags = cardDataList.Find(card => card.DbfId == tmpCard.DbfId.ToString()).WikiTagsList?.Select(tag => new TagContent { Name = tag, ItemType = "WikiTags" }).ToList() ?? new List<TagContent>();
                    keywords = cardDataList.Find(card => card.DbfId == tmpCard.DbfId.ToString()).KeywordsList?.Select(tag => new TagContent { Name = tag, ItemType = "Keywords" }).ToList() ?? new List<TagContent>();
                    races = cardDataList.Find(card => card.DbfId == tmpCard.DbfId.ToString()).RacesList?.Select(tag => new TagContent { Name = tag, ItemType = "Races" }).ToList() ?? new List<TagContent>();
                    wikiMechanics = cardDataList.Find(card => card.DbfId == tmpCard.DbfId.ToString()).WikiMechanicsList?.Select(tag => new TagContent { Name = tag, ItemType = "WikiMechanics" }).ToList() ?? new List<TagContent>();
                }

                // merge wikiTags, keywords, races, wikiMechanics
                var tags = races.Concat(wikiTags).Concat(wikiMechanics).Concat(keywords).ToList();
                foreach (var tag in tags)
                {
                    tag.ForeColor = colorDictionary[tag.ItemType][0];
                    tag.BkgColor = colorDictionary[tag.ItemType][1];
                }

                var tags1 = tags.Take(tags.Count / 2).ToList();
                var tags2 = tags.Skip(tags.Count / 2).ToList();
                _cardResults.Add(
                    new CardResult
                    {
                        ImageSource = image,
                        DisplayText = tmpCard.GetLocName(_locale),
                        Tags1 = tags1,
                        Tags2 = tags2
                    }
                ); // 添加到集合
            }
        }

        private static async Task<BitmapImage> GetCardImageAsync(string cardId)
        {
            while (true)
            {
                // 如果image文件夹不存在，则创建
                if (!Directory.Exists("image"))
                {
                    Directory.CreateDirectory("image");
                }

                var imagePath = System.IO.Path.Combine("image", $"{cardId}.jpg");

                // 检查文件是否存在
                if (File.Exists(imagePath))
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 确保图像在加载后可用
                    bitmapImage.EndInit();
                    bitmapImage.Freeze(); // 使图像可以在不同线程中使用
                    return bitmapImage;
                }
                else
                {
                    // 下载图片并保存
                    var url = $"https://static.zerotoheroes.com/hearthstone/cardart/256x/{cardId}.jpg";
                    var imageBytes = await HttpClient.GetByteArrayAsync(url);

                    // 使用 FileStream 异步写入文件
                    using (var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                    {
                        await fileStream.WriteAsync(imageBytes, 0, imageBytes.Length);
                    }
                }
            }
        }

        private void Image_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!(sender is Image image)) return;
            // 创建一个新的窗口来显示放大的图片
            var popup = new Window
            {
                Width = 200,
                Height = 200,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Content = new Image
                {
                    Source = image.Source,
                    Stretch = Stretch.Uniform,
                    Width = 200,
                    Height = 200,
                },
            };

            // 获取图片在屏幕上的位置
            var position = image.PointToScreen(new Point(0, 0));
            popup.Left = (position.X + (image.ActualWidth / 2) - (popup.Width / 2)) / 2 + 10;
            popup.Top = (position.Y + image.ActualHeight) / 2 + 35;

            popup.Show();
            image.Tag = popup; // 保存窗口引用
        }

        private void Image_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!(sender is Image image) || !(image.Tag is Window popup)) return;
            popup.Close(); // 关闭放大窗口
            image.Tag = null; // 清除引用
        }

        private void AddItemToSearchFilter(object sender, MouseButtonEventArgs e)
        {
            // 创建新的 SearchItem 并设置颜色和类型
            var border = sender as Border;
            var item = border.Child as TextBlock;
            var newItem = new SearchItem
            {
                Name = item.Text, // 设置名称
                ForeColor = colorDictionary[border.Tag as string][0],
                BkgColor = colorDictionary[border.Tag as string][1],
                ItemType = border.Tag as string // 设置类型为 tags 或 races
            };

            // 检查 SearchFilterItems 中是否已经存在项
            if (!_searchFilterItems.Any(i => i.Name == newItem.Name && i.ItemType == newItem.ItemType))
            {
                // 将新项添加到 SearchFilterItems
                _searchFilterItems.Add(newItem);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取被点击的按钮
            var closeButton = sender as Button;
            // 获取绑定的项
            var item = closeButton?.Tag; // 获取 Tag 中的值

            // 从 SearchFilterItems 中移除该项
            if (item != null && _searchFilterItems.Contains(item as SearchItem))
            {
                _searchFilterItems.Remove(item as SearchItem);
            }
        }

        // ... existing code ...
        private void DropBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingFromDropBox) return; // 如果正在选择下拉框中的项，则返回
            var inputText = DropBox.Text;

            if (inputText.Length >= 2)
            {
                var matches = _cardSearcher.combinedList
                    .Where(item => item.Item1.StartsWith(inputText, StringComparison.OrdinalIgnoreCase)) // 匹配每个 item 的第一项
                    .Select(item => item.Item1) // 选择匹配的项
                    .Distinct() // 去重
                    .ToList();

                DropBox.ItemsSource = matches; // 更新下拉列表
                DropBox.IsDropDownOpen = true;
            }
            else
            {
                DropBox.ItemsSource = null; // 清空下拉列表
            }
        }


        private async void SearchFilterItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 清空之前的结果
            _cardResults.Clear();
            // _searchFilterItems to TagContent list
            var tags = _searchFilterItems.ToList().Select(item => new TagContent { Name = item.Name, ItemType = item.ItemType, ForeColor = item.ForeColor, BkgColor = item.BkgColor }).ToList();

            // 提取所有标签
            await SearchByTags(tags);
        }

        private async Task SearchByTags(List<TagContent> tags)
        {
            // 清空之前的结果
            _cardResults.Clear();

            // 根据标签进行搜索的逻辑
            // 这里假设您有一个方法可以根据标签获取卡片
            var resultList = await _cardSearcher.GetCardsByTags(tags);
            var cardDataList = _cardSearcher.CardDataList;

            // 遍历结果并下载��片
            foreach (var tmpCard in resultList)
            {
                var image = await GetCardImageAsync(tmpCard.Id); // 获取卡片图片
                var keywords = cardDataList.Find(card => card.Id == tmpCard.Id).KeywordsList?.Select(tag => new TagContent { Name = tag, ItemType = "Keywords" }).ToList() ?? new List<TagContent>();
                var races = cardDataList.Find(card => card.Id == tmpCard.Id).RacesList?.Select(tag => new TagContent { Name = tag, ItemType = "Races" }).ToList() ?? new List<TagContent>();
                var wikiTags = cardDataList.Find(card => card.Id == tmpCard.Id).WikiTagsList?.Select(tag => new TagContent { Name = tag, ItemType = "WikiTags" }).ToList() ?? new List<TagContent>();
                var wikiMechanics = cardDataList.Find(card => card.Id == tmpCard.Id).WikiMechanicsList?.Select(tag => new TagContent { Name = tag, ItemType = "WikiMechanics" }).ToList() ?? new List<TagContent>();

                // merge wikiTags, keywords, races, wikiMechanics
                var ResultTags = races.Concat(wikiTags).Concat(wikiMechanics).Concat(keywords).ToList();
                foreach (var tag in ResultTags)
                {
                    tag.ForeColor = colorDictionary[tag.ItemType][0];
                    tag.BkgColor = colorDictionary[tag.ItemType][1];
                }

                var dbCard = _cardSearcher.GetBaconCards().FirstOrDefault(card => card.Id == tmpCard.Id);
                var tags1 = ResultTags.Take(ResultTags.Count / 2).ToList();
                var tags2 = ResultTags.Skip(ResultTags.Count / 2).ToList();
                _cardResults.Add(
                    new CardResult
                    {
                        ImageSource = image,
                        DisplayText = dbCard?.GetLocName(_locale),
                        Tags1 = tags1,
                        Tags2 = tags2
                    }
                ); // 添加到集合
            }
        }

        private string GetItemType(string selectedItem)
        {
            if (_cardSearcher.AllWikiTagsList.Contains(selectedItem))
            {
                return "WikiTags";
            }
            else if (_cardSearcher.AllKeywordsList.Contains(selectedItem))
            {
                return "Keywords";
            }
            else if (_cardSearcher.AllWikiMechanicsList.Contains(selectedItem))
            {
                return "WikiMechanics";
            }
            else if (_cardSearcher.AllRacesList.Contains(selectedItem))
            {
                return "Races";
            }
            return "Error";
        }

        private void DropBoxTextInput_KeyDown(object sender, KeyEventArgs e) // 添加键盘事件处理
        {
            if (e.Key == Key.Enter && DropBox.SelectedItem != null) // 检查是否按下回车并且有选中的项目
            {
                var selectedItem = DropBox.SelectedItem.ToString(); // 获取选中的项目
                DropBox.Text = selectedItem; // 设置组合框的文字为选中的项目

                var ItemType = GetItemType(selectedItem);
                var newItem = new SearchItem
                {
                    Name = selectedItem, // 设置名称
                    ForeColor = colorDictionary[ItemType][0],
                    BkgColor = colorDictionary[ItemType][1],
                    ItemType = ItemType
                };

                // 检查 SearchFilterItems 中是否已经存在项
                if (!_searchFilterItems.Any(i => i.Name == newItem.Name && i.ItemType == newItem.ItemType))
                {
                    // 将新项添加到 SearchFilterItems
                    _searchFilterItems.Add(newItem);
                }
            }
        }

        private void DropBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isSelectingFromDropBox = false; // 在选择更改时重置标志
        }

        private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _searchFilterItems.Clear(); // 清空过滤器项
            // 如果需要，您还可以清空其他相关的 UI 元素，例如 ResultsListView
            _cardResults.Clear(); // 清空搜索结果
        }
    }
}
