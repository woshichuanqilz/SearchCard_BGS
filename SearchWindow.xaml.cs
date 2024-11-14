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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HearthDb;
using HearthDb.Enums;

namespace CardSearcher
{
    public class SearchItem : INotifyPropertyChanged
    {
        private string _name;
        private SolidColorBrush _color;
        private SolidColorBrush _foreColor;
        public string ItemType { get; set; }

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

        public SolidColorBrush Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged(nameof(Color));
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
        private static readonly HttpClient httpClient = new HttpClient();
        private ObservableCollection<CardResult> cardResults;
        private ObservableCollection<SearchItem> SearchFilterItems;
        private CardSearcher cardSearcher;

        public SearchWindow()
        {
            InitializeComponent();
            InputBox.TextChanged += InputBox_TextChanged;
            cardResults = new ObservableCollection<CardResult>();
            ResultsListView.ItemsSource = cardResults;
            SearchFilterItems = new ObservableCollection<SearchItem>();
            SearchFilter.ItemsSource = SearchFilterItems;

            // 订阅 CollectionChanged 事件
            SearchFilterItems.CollectionChanged += SearchFilterItems_CollectionChanged;

            cardSearcher = new CardSearcher();
            Task.Run(async () => await cardSearcher.Run()); // 使用 Task.Run 来处理异步调用
        }

        // async method
        private async void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string tmp_Id = InputBox.Text;
            if (tmp_Id.Length >= 3)
            {
                // 获取匹配的卡片
                var resultList = new List<Card>();
                bool is_bg = tmp_Id.StartsWith("bg");
                bool is_number = tmp_Id.All(char.IsDigit);
                // tmp_Id is start with "bg"
                if (is_bg)
                {
                    resultList = await Task.Run(
                        () =>
                            cardSearcher
                                .GetBaconCards()
                                .Where(kvp => kvp.Id.ToLower().Contains(tmp_Id.ToLower()))
                                .ToList()
                    );
                }
                // tmp_Id is number
                else if (is_number)
                {
                    resultList = await Task.Run(
                        () =>
                            cardSearcher
                                .GetBaconCards()
                                .Where(kvp => kvp.DbfId.ToString().Contains(tmp_Id))
                                .ToList()
                    );
                }

                var cardDataList = cardSearcher.CardDataList;
                // 输出结果数量到控制台
                Console.WriteLine($"resultList.count: {resultList.Count}");

                // 清空之前的结果
                cardResults.Clear();

                // 遍历结果并下载图片
                foreach (var tmpCard in resultList)
                {
                    Console.WriteLine($"tmpCard.Id: {tmpCard.Id}");
                    var image = await GetCardImageAsync(tmpCard.Id); // 获取卡片图片
                    var tags = new List<string>();
                    var keywords = new List<string>();
                    var races = new List<string>();
                    if (is_bg)
                    {
                        // wikitags if wikiTagsList is null, create a new list
                        tags =
                            cardDataList.Find(card => card.id == tmpCard.Id).wikiTagsList
                            ?? new List<string>();
                        // add keywordsList if keywordsList is not null
                        keywords = cardDataList.Find(card => card.id == tmpCard.Id).KeywordsList;
                        races = cardDataList.Find(card => card.id == tmpCard.Id).RacesList;
                    }
                    else if (is_number)
                    {
                        tags =
                            cardDataList
                                .Find(card => card.dbfId == tmpCard.DbfId.ToString())
                                .wikiTagsList ?? new List<string>();
                        keywords = cardDataList
                            .Find(card => card.dbfId == tmpCard.DbfId.ToString())
                            .KeywordsList;
                        races = cardDataList
                            .Find(card => card.dbfId == tmpCard.DbfId.ToString())
                            .RacesList;
                    }

                    if (keywords != null)
                    {
                        tags.AddRange(keywords);
                    }

                    cardResults.Add(
                        new CardResult
                        {
                            ImageSource = image,
                            DisplayText = tmpCard.GetLocName(Locale.zhCN),
                            Tags = tags,
                            Races = races,
                        }
                    ); // 添加到集合
                }
            }
        }

        private async Task<BitmapImage> GetCardImageAsync(string cardId)
        {
            // 如果image文件夹不存在，则创建
            if (!Directory.Exists("image"))
            {
                Directory.CreateDirectory("image");
            }

            string imagePath = System.IO.Path.Combine("image", $"{cardId}.jpg");

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
                var imageBytes = await httpClient.GetByteArrayAsync(url);

                // 使用 FileStream 异步写入文件
                using (
                    var fileStream = new FileStream(
                        imagePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        useAsync: true
                    )
                )
                {
                    await fileStream.WriteAsync(imageBytes, 0, imageBytes.Length);
                }

                return await GetCardImageAsync(cardId); // 递归调用以获取新下载的图像
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string input = InputBox.Text;
            // Perform search action with the input value
        }

        private void Image_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
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
        }

        private void Image_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var image = sender as Image;
            if (image != null && image.Tag is Window popup)
            {
                popup.Close(); // 关闭放大窗口
                image.Tag = null; // 清除引用
            }
        }

        private void RaceItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var clickedItem = (sender as Border)?.DataContext; // 获取被点击的项目
            if (clickedItem != null)
            {
                // 将点击的项目添加到 SearchFilter 中
                AddItemToSearchFilter(
                    new SearchItem
                    {
                        Name = clickedItem.ToString(),
                        Color = Brushes.Blue,
                        ForeColor = Brushes.White,
                        ItemType = "Race",
                    }
                );
            }
        }

        private void Tags_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var clickedItem = (sender as Border)?.DataContext; // 获取被点击的项目
            if (clickedItem != null)
            {
                AddItemToSearchFilter(
                    new SearchItem
                    {
                        Name = clickedItem.ToString(),
                        Color = Brushes.Orange,
                        ForeColor = Brushes.Black,
                        ItemType = "Tag",
                    }
                );
            }
        }

        private void AddItemToSearchFilter(object clickedItem)
        {
            // 假设 clickedItem 是 SearchItem 类型
            if (clickedItem is SearchItem item)
            {
                // 创建新的 SearchItem 并设置颜色和类型
                var newItem = new SearchItem
                {
                    Name = item.Name, // 设置名称
                    Color = item.Color,
                    ForeColor = item.ForeColor,
                    ItemType = item.ItemType // 设置类型为 tags 或 races
                };

                // 检查 SearchFilterItems 中是否已经存在该项
                if (!SearchFilterItems.Any(i => i.Name == newItem.Name && i.ItemType == newItem.ItemType))
                {
                    // 将新项添加到 SearchFilterItems
                    SearchFilterItems.Add(newItem);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取被点击的按钮
            Button closeButton = sender as Button;
            if (closeButton != null)
            {
                // 获取绑定的项
                var item = closeButton.Tag; // 获取 Tag 中的值

                // 从 SearchFilterItems 中移除该项
                if (item != null && SearchFilterItems.Contains(item as SearchItem))
                {
                    SearchFilterItems.Remove(item as SearchItem);
                }
            }
        }

        // 用于存储卡片结果的类
        public class CardResult
        {
            public BitmapImage ImageSource { get; set; }
            public string DisplayText { get; set; }

            // 用于存储标签的列表
            public List<string> Tags { get; set; } = new List<string>(); // 初始化一个空列表
            public List<string> Races { get; set; } = new List<string>(); // 初始化为一个空列表
        }

        private async void SearchFilterItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 清空之前的结果
            cardResults.Clear();

            // 提取所有标签
            var tags = SearchFilterItems
                .Where(item => item.ItemType == "Tag") // 只提取标签
                .Select(item => item.Name)
                .ToList();

            // 需要把种族也提取出来
            var races = SearchFilterItems
                .Where(item => item.ItemType == "Race") // 只提取种族
                .Select(item => item.Name)
                .ToList();
            await SearchByTags(tags, races);
        }

        private async Task SearchByTags(List<string> tags, List<string> races)
        {
            // 清空之前的结果
            cardResults.Clear();

            // 根据标签进行搜索的逻辑
            // 这里假设您有一个方法可以根据标签获取卡片
            var resultList = await cardSearcher.GetCardsByTagsAndRaces(tags, races);


            // 遍历结果并下载图片
            foreach (var tmpCard in resultList)
            {
                var image = await GetCardImageAsync(tmpCard.id); // 获取卡片图片
                var dbCard = cardSearcher.GetBaconCards().Where(card => card.Id == tmpCard.id).FirstOrDefault();
                cardResults.Add(
                    new CardResult
                    {
                        ImageSource = image,
                        DisplayText = dbCard.GetLocName(Locale.zhCN),
                        Tags = tmpCard.wikiTagsList,
                        // 如果 RacesList 的第一个元素为空，则设置为 "Neutral"
                        Races = tmpCard.RacesList
                    }
                ); // 添加到集合
            }
        }
    }
}
