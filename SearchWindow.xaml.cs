using HearthDb;
using System;
using System.Collections.Generic;
using System.Linq;
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
using System.IO;
using System.Net.Http;
using System.Collections.ObjectModel;
using HearthDb.Enums;

namespace CardSearcher
{
    /// <summary>
    /// SearchWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SearchWindow 
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private ObservableCollection<CardResult> cardResults;
        private CardSearcher cardSearcher;

        public SearchWindow()
        {
            InitializeComponent();
            InputBox.TextChanged += InputBox_TextChanged;
            cardResults = new ObservableCollection<CardResult>();
            ResultsListView.ItemsSource = cardResults;
            cardSearcher = new CardSearcher();
            Task.Run(async () => await cardSearcher.Run()); // 使用 Task.Run 来处理异步调用
        }

        // async method 
        private async void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string cardId = InputBox.Text;
            if (cardId.Length >= 3)
            {
                // 获取匹配的卡片
                var resultList = await Task.Run(() => cardSearcher.GetBaconCards()
                    .Where(kvp => kvp.Id.ToLower().Contains(cardId.ToLower()))
                    .ToList());
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
                    // wikitags if wikiTagsList is null, create a new list
                    var tags = cardDataList.Find(card => card.id == tmpCard.Id).wikiTagsList ?? new List<string>();
                    // add keywordsList if keywordsList is not null
                    var keywords = cardDataList.Find(card => card.id == tmpCard.Id).KeywordsList;
                    if (keywords != null)
                    {
                        tags.AddRange(keywords);
                    }
                    cardResults.Add(new CardResult { ImageSource = image, DisplayText = tmpCard.GetLocName(Locale.zhCN), Tags = tags }); // 添加到集合
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
                using (var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
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
    }

    // 用于存储卡片结果的类
    public class CardResult
    {
        public BitmapImage ImageSource { get; set; }
        public string DisplayText { get; set; }

        // 用于存储标签的列表
        public List<string> Tags { get; set; } = new List<string>(); // 初始化为一个空列表
    }
}
