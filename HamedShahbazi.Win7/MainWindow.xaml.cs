using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;
using Microsoft.Win32;

using iTextSharp.text;
using iTextSharp.text.pdf;
namespace HamedShahbazi.Win7
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            InitializeDashboard();
            InitializeDayCount();

            LoadSettings();
            LoadOfficerData();
            LoadCurrentProgram();
            LoadProgramHistory();
        }
        private List<Officer> guardOfficers =
            new List<Officer>();
        private bool isTestDataActive = false;
        private List<Officer> reserveOfficers =
            new List<Officer>();

        private List<Officer> chiefOfficers =
            new List<Officer>();
        // =====================================================
        // لیست مستقل نیروهای جایگزین
        // این لیست از سه لیست اصلی ساخته می‌شود.
        // =====================================================
        private List<ReplacementOfficerRow> replacementOfficers =
            new List<ReplacementOfficerRow>();
        // =====================================================
        // رابط کاربری یک امتیاز در راهنما
        // =====================================================

        private class GuideScoreUI
        {
            public TextBlock ScoreLabel { get; set; }

            public TextBlock DescriptionLabel { get; set; }

            public TextBlock CurrentLabel { get; set; }

            public TextBlock CapacityLabel { get; set; }

            public TextBlock RemainingLabel { get; set; }

            public TextBlock StatusLabel { get; set; }

            public TextBlock PercentLabel { get; set; }

            public ProgressBar ProgressBar { get; set; }
        }

        // =====================================================
        // رابط کاربری یک کارت راهنما
        // =====================================================

        private class GuideCardUI
        {
            public Border Card { get; set; }

            public Dictionary<double, GuideScoreUI> Scores { get; set; }
                = new Dictionary<double, GuideScoreUI>();
        }




        private readonly ScheduleGenerator scheduleGenerator =
            new ScheduleGenerator();

        private List<GuardScheduleItem> schedule =
            new List<GuardScheduleItem>();

        // =====================================================
        // تاریخچه دائمی برنامه‌های ساخته‌شده
        // =====================================================
        private List<GuardProgramHistory> savedPrograms =
            new List<GuardProgramHistory>();

        private Grid scheduleGrid = null;

        private Dictionary<int, ScheduleDayUI> scheduleDayCache =
            new Dictionary<int, ScheduleDayUI>();

        private bool isScheduleUIInitialized = false;

        private DateTime currentProgramStartDate =
            DateTime.Now.Date;
        private int selectedProgramDays = 31;

        private bool useReplacementOfficers = false;
        private GuideCardUI guardGuideCard = null;

        private GuideCardUI reserveGuideCard = null;

        private GuideCardUI chiefGuideCard = null;

        private bool isGuideUIInitialized = false;
        private int replacementCountPerGroup = 1;
        private readonly HashSet<string> usedReplacementGuards =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> usedReplacementReserves =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> usedReplacementChiefs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Random replacementRandom =
            new Random();
        private bool notificationsEnabled = true;



        private bool isLoadingSettings = false;
        [Serializable]
        public class AppSettings
        {
            public int ProgramDays { get; set; }

            public bool ReplacementEnabled { get; set; }

            public int ReplacementCount { get; set; }

            public bool NotificationsEnabled { get; set; }

            public bool TestDataActive { get; set; }

            public AppSettings()
            {
                ProgramDays = 31;
                ReplacementEnabled = false;
                ReplacementCount = 1;
                NotificationsEnabled = true;
                TestDataActive = false;
            }
        }


        // =====================================================
        // مدل یک برنامه ذخیره‌شده در تاریخچه
        // =====================================================
        [Serializable]
        public class GuardProgramHistory
        {
            public string Id { get; set; }

            public DateTime CreatedAt { get; set; }

            public DateTime StartDate { get; set; }

            public int ProgramDays { get; set; }

            public int PassCount { get; set; }

            public int HolidayCount { get; set; }

            public bool ReplacementEnabled { get; set; }

            public int ReplacementCount { get; set; }

            public List<GuardScheduleItem> Schedule { get; set; }

            public GuardProgramHistory()
            {
                Id = Guid.NewGuid().ToString();

                Schedule =
                    new List<GuardScheduleItem>();
            }
        }

        // =====================================================
        // فایل اصلی تاریخچه
        // =====================================================
        [Serializable]
        public class ProgramHistoryStorage
        {
            public List<GuardProgramHistory> Programs { get; set; }

            public ProgramHistoryStorage()
            {
                Programs =
                    new List<GuardProgramHistory>();
            }
        }

        private class ReportDataResult
        {
            public List<OfficerReportRow> Reserve { get; set; }

            public List<OfficerReportRow> Chief { get; set; }

            public List<OfficerReportRow> Guard { get; set; }

            public ReportDataResult()
            {
                Reserve = new List<OfficerReportRow>();
                Chief = new List<OfficerReportRow>();
                Guard = new List<OfficerReportRow>();
            }
        }
        private async Task UpdateReportProgress(
            double target,
            string message,
            string status)
        {
            ReportsLoadingText.Text = message;
            ReportsLoadingStatus.Text = status;

            double start =
                ReportsLoadingProgress.Value;

            double difference =
                target - start;

            int steps = 25;

            for (int i = 1; i <= steps; i++)
            {
                double value =
                    start +
                    (difference * i / steps);

                ReportsLoadingProgress.Value =
                    value;

                ReportsLoadingPercent.Text =
                    Math.Round(value)
                        .ToString("0") + "%";

                // اجازه Render شدن UI
                await Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Render);

                await Task.Delay(12);
            }
        }
        private void SaveSettings()
        {
            try
            {
                AppSettings settings =
                    new AppSettings();

                settings.ProgramDays =
                    selectedProgramDays;

                settings.ReplacementEnabled =
                    useReplacementOfficers;

                settings.ReplacementCount =
                    replacementCountPerGroup;

                settings.NotificationsEnabled =
                    notificationsEnabled;

                settings.TestDataActive =
                    isTestDataActive;

                XmlSerializer serializer =
                    new XmlSerializer(
                        typeof(AppSettings));

                using (FileStream stream =
                       new FileStream(
                           GetSettingsDataFile(),
                           FileMode.Create,
                           FileAccess.Write))
                {
                    serializer.Serialize(
                        stream,
                        settings);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ذخیره تنظیمات:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private class SchedulePassUI
        {
            public Border Card { get; set; }

            public TextBlock PassLabel { get; set; }

            public TextBlock PersonLabel { get; set; }

            public TextBlock ScoreLabel { get; set; }
        }

        private class ScheduleDayUI
        {
            public Border Card { get; set; }

            public TextBlock DayLabel { get; set; }

            public TextBlock DateLabel { get; set; }

            public TextBlock HolidayLabel { get; set; }

            public List<SchedulePassUI> Passes { get; set; }
                = new List<SchedulePassUI>();
        }
        // =====================================================
        // اطلاعات یک ردیف گزارش
        // =====================================================

        private class OfficerReportRow
        {
            public int RowNumber { get; set; }

            public string Name { get; set; }

            public string Score { get; set; }

            public int PassCount { get; set; }

            public string PassDays { get; set; }

            public string Status { get; set; }

            public string Holidays { get; set; }

            public OfficerReportRow()
            {
                Name = "";
                Score = "";
                PassDays = "";
                Status = "";
                Holidays = "";
            }
        }

        // =====================================================
        // اطلاعات یک نیروی جایگزین
        // =====================================================
        private class ReplacementOfficerRow
        {
            public int RowNumber { get; set; }

            public string Name { get; set; }

            public string Score { get; set; }

            public string Role { get; set; }

            public string Status { get; set; }

            public ReplacementOfficerRow()
            {
                Name = "";
                Score = "";
                Role = "";
                Status = "";
            }
        }

        // =====================================================
        // ساخت لیست مستقل نیروهای جایگزین
        // =====================================================
        private void UpdateReplacementOfficersList()
        {
            replacementOfficers.Clear();

            // اگر تیک خاموش است،
            // لیست جایگزین باید کاملاً خالی باشد.
            if (!useReplacementOfficers)
                return;

            int row = 1;

            // ==========================================
            // افسر پاسدار
            // ==========================================
            foreach (Officer officer in guardOfficers)
            {
                if (officer == null)
                    continue;

                if (!officer.IsReplacement)
                    continue;

                replacementOfficers.Add(
                    new ReplacementOfficerRow
                    {
                        RowNumber = row++,
                        Name = officer.Name,
                        Score = officer.Score.ToString(),
                        Role = "افسر پاسدار",
                        Status = "🔄 نیروی جایگزین"
                    });
            }

            // ==========================================
            // افسر جانشین
            // ==========================================
            foreach (Officer officer in reserveOfficers)
            {
                if (officer == null)
                    continue;

                if (!officer.IsReplacement)
                    continue;

                replacementOfficers.Add(
                    new ReplacementOfficerRow
                    {
                        RowNumber = row++,
                        Name = officer.Name,
                        Score = officer.Score.ToString(),
                        Role = "افسر جانشین",
                        Status = "🔄 نیروی جایگزین"
                    });
            }

            // ==========================================
            // افسر سر
            // ==========================================
            foreach (Officer officer in chiefOfficers)
            {
                if (officer == null)
                    continue;

                if (!officer.IsReplacement)
                    continue;

                replacementOfficers.Add(
                    new ReplacementOfficerRow
                    {
                        RowNumber = row++,
                        Name = officer.Name,
                        Score = officer.Score.ToString(),
                        Role = "افسر سر",
                        Status = "🔄 نیروی جایگزین"
                    });
            }
        }
        // =====================================================
        // محاسبه ظرفیت امتیازها
        // =====================================================

        private Dictionary<double, int> CalculateScoreCapacity()
        {
            Dictionary<double, int> result =
                new Dictionary<double, int>();

            result[0.5] = 0;
            result[1.0] = 0;
            result[2.0] = 0;


            if (selectedProgramDays <= 0)
                return result;


            DateTime startDate =
                currentProgramStartDate.Date;


            for (int i = 0;
                 i < selectedProgramDays;
                 i++)
            {
                DateTime date =
                    startDate.AddDays(i);


                // ==========================================
                // امتیاز 0.5
                // پنجشنبه + جمعه
                // ==========================================

                if (date.DayOfWeek == DayOfWeek.Thursday ||
                    date.DayOfWeek == DayOfWeek.Friday)
                {
                    result[0.5]++;
                }


                // ==========================================
                // امتیاز 1
                // شنبه + یکشنبه + دوشنبه
                // ==========================================

                else if (date.DayOfWeek == DayOfWeek.Saturday ||
                         date.DayOfWeek == DayOfWeek.Sunday ||
                         date.DayOfWeek == DayOfWeek.Monday)
                {
                    result[1.0]++;
                }


                // ==========================================
                // امتیاز 2
                // سه‌شنبه + چهارشنبه
                // ==========================================

                else if (date.DayOfWeek == DayOfWeek.Tuesday ||
                         date.DayOfWeek == DayOfWeek.Wednesday)
                {
                    result[2.0]++;
                }
            }


            return result;
        }

        // =====================================================
        // بررسی ظرفیت نیروها قبل از ایجاد برنامه
        // =====================================================
        private bool ValidateOfficerCapacity()
        {
            Dictionary<double, int> capacity =
                CalculateScoreCapacity();

            // ==========================================
            // تابع ساخت پیام خطا برای یک لیست
            // ==========================================
            string BuildOverCapacityMessage(
                string role,
                List<Officer> officers)
            {
                List<string> errors =
                    new List<string>();

                double[] scores =
                    new double[] { 0.5, 1.0, 2.0 };

                foreach (double score in scores)
                {
                    int currentCount =
                        officers.Count(
                            delegate (Officer officer)
                            {
                                return officer.Score == score;
                            });

                    int maxCapacity =
                        capacity.ContainsKey(score)
                            ? capacity[score]
                            : 0;

                    if (currentCount > maxCapacity)
                    {
                        int excess =
                            currentCount - maxCapacity;

                        errors.Add(
                            "امتیاز " +
                            score.ToString() +
                            " : " +
                            currentCount.ToString() +
                            " نفر " +
                            " (ظرفیت مجاز: " +
                            maxCapacity.ToString() +
                            " نفر، " +
                            "مازاد: " +
                            excess.ToString() +
                            " نفر)");
                    }
                }

                if (errors.Count == 0)
                    return "";

                return
                    "❌ " + role + "\n" +
                    string.Join("\n", errors);
            }


            // ==========================================
            // بررسی سه لیست
            // ==========================================

            List<string> allErrors =
                new List<string>();

            string guardError =
                BuildOverCapacityMessage(
                    "افسر پاسدار",
                    guardOfficers);

            if (!string.IsNullOrWhiteSpace(guardError))
                allErrors.Add(guardError);


            string reserveError =
                BuildOverCapacityMessage(
                    "افسر جانشین",
                    reserveOfficers);

            if (!string.IsNullOrWhiteSpace(reserveError))
                allErrors.Add(reserveError);


            string chiefError =
                BuildOverCapacityMessage(
                    "افسر سر",
                    chiefOfficers);

            if (!string.IsNullOrWhiteSpace(chiefError))
                allErrors.Add(chiefError);


            // ==========================================
            // اگر هیچ خطایی نیست
            // ==========================================

            if (allErrors.Count == 0)
                return true;


            // ==========================================
            // نمایش خطا
            // ==========================================

            MessageBox.Show(
                "امکان ایجاد برنامه وجود ندارد.\n\n" +
                "ظرفیت مجاز در راهنما رعایت نشده است.\n\n" +
                string.Join(
                    "\n\n",
                    allErrors) +
                "\n\n" +
                "لطفاً تعداد نیروها یا امتیازها را اصلاح کنید.",
                "ظرفیت نیروها بیشتر از حد مجاز است",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }
        private GuideCardUI CreateGuideCard(
            string icon,
            string title,
            string color)
        {
            System.Windows.Media.Color themeColor =
                (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter
                    .ConvertFromString(color);


            // =====================================================
            // کارت اصلی
            // =====================================================

            Border card =
                new Border();

            card.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#FFFFFF"));

            card.BorderBrush =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E2E8F0"));

            card.BorderThickness =
                new Thickness(1);

            card.CornerRadius =
                new CornerRadius(16);

            card.Padding =
                new Thickness(10);

            card.Margin =
                new Thickness(6);


            StackPanel main =
                new StackPanel();


            // =====================================================
            // هدر
            // =====================================================

            Border header =
                new Border();

            header.Background =
                new System.Windows.Media.SolidColorBrush(
                    themeColor);

            header.CornerRadius =
                new CornerRadius(11);

            header.Height =
                58;


            StackPanel headerPanel =
                new StackPanel();

            headerPanel.Orientation =
                Orientation.Horizontal;

            headerPanel.HorizontalAlignment =
                HorizontalAlignment.Center;

            headerPanel.VerticalAlignment =
                VerticalAlignment.Center;


            TextBlock iconLabel =
                new TextBlock();

            iconLabel.Text =
                icon;

            iconLabel.FontSize =
                23;

            iconLabel.VerticalAlignment =
                VerticalAlignment.Center;


            TextBlock titleLabel =
                new TextBlock();

            titleLabel.Text =
                title;

            titleLabel.FontSize =
                18;

            titleLabel.FontWeight =
                FontWeights.Bold;

            titleLabel.Foreground =
                System.Windows.Media.Brushes.White;

            titleLabel.VerticalAlignment =
                VerticalAlignment.Center;

            titleLabel.Margin =
                new Thickness(9, 0, 0, 0);


            headerPanel.Children.Add(iconLabel);
            headerPanel.Children.Add(titleLabel);

            header.Child =
                headerPanel;


            main.Children.Add(header);


            // =====================================================
            // جداکننده
            // =====================================================

            Border separator =
                new Border();

            separator.Height =
                1;

            separator.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E5E7EB"));

            separator.Margin =
                new Thickness(5, 14, 5, 11);


            main.Children.Add(separator);


            // =====================================================
            // سه امتیاز
            // =====================================================

            GuideCardUI guideCard =
                new GuideCardUI();


            double[] scores =
                new double[]
                {
            0.5,
            1.0,
            2.0
                };


            foreach (double score in scores)
            {
                GuideScoreUI scoreUI =
                    CreateGuideScoreCard(
                        score,
                        color);


                guideCard.Scores[score] =
                    scoreUI;


                main.Children.Add(
                    CreateGuideScoreBorder(
                        scoreUI,
                        color));
            }


            card.Child =
                main;


            guideCard.Card =
                card;


            return guideCard;
        }
        private GuideScoreUI CreateGuideScoreCard(
          double score,
          string themeColor)
        {
            GuideScoreUI result =
                new GuideScoreUI();


            // =====================================================
            // متن امتیاز
            // =====================================================

            string scoreText;

            if (score == 0.5)
            {
                scoreText = "۰٫۵";
            }
            else if (score == 1.0)
            {
                scoreText = "۱";
            }
            else
            {
                scoreText = "۲";
            }


            TextBlock scoreLabel =
                new TextBlock();

            scoreLabel.Text =
                "امتیاز " + scoreText;

            scoreLabel.FontSize =
                16;

            scoreLabel.FontWeight =
                FontWeights.Bold;

            scoreLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString(themeColor));

            scoreLabel.HorizontalAlignment =
                HorizontalAlignment.Left;


            // =====================================================
            // توضیح
            // =====================================================

            string description;

            if (score == 0.5)
            {
                description =
                    "پنجشنبه و جمعه • یک پاس در هفته";
            }
            else if (score == 1.0)
            {
                description =
                    "شنبه تا دوشنبه • دو پاس در هفته";
            }
            else
            {
                description =
                    "سه‌شنبه و چهارشنبه • سه پاس در هفته";
            }


            TextBlock descriptionLabel =
                new TextBlock();

            descriptionLabel.Text =
                description;

            descriptionLabel.FontSize =
                11;

            descriptionLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#64748B"));

            descriptionLabel.Margin =
                new Thickness(0, 4, 0, 0);

            descriptionLabel.HorizontalAlignment =
                HorizontalAlignment.Left;


            // =====================================================
            // تعداد فعلی
            // =====================================================

            TextBlock currentLabel =
                new TextBlock();

            currentLabel.Text =
                "تعداد فعلی: ۰ نفر";

            currentLabel.FontSize =
                12;

            currentLabel.FontWeight =
                FontWeights.SemiBold;

            currentLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#334155"));

            currentLabel.Margin =
                new Thickness(0, 10, 0, 0);

            currentLabel.HorizontalAlignment =
                HorizontalAlignment.Left;


            // =====================================================
            // ظرفیت
            // =====================================================

            TextBlock capacityLabel =
                new TextBlock();

            capacityLabel.Text =
                "ظرفیت: ۰ نفر";

            capacityLabel.FontSize =
                12;

            capacityLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#475569"));

            capacityLabel.Margin =
                new Thickness(0, 2, 0, 0);

            capacityLabel.HorizontalAlignment =
                HorizontalAlignment.Left;


            // =====================================================
            // باقی مانده
            // =====================================================

            TextBlock remainingLabel =
                new TextBlock();

            remainingLabel.Text =
                "ظرفیت باقی‌مانده: ۰ نفر";

            remainingLabel.FontSize =
                12;

            remainingLabel.FontWeight =
                FontWeights.SemiBold;

            remainingLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#059669"));

            remainingLabel.Margin =
                new Thickness(0, 3, 0, 0);

            remainingLabel.HorizontalAlignment =
                HorizontalAlignment.Left;


            // =====================================================
            // وضعیت
            // =====================================================

            TextBlock statusLabel =
                new TextBlock();

            statusLabel.Text =
                "✓ ظرفیت مجاز";

            statusLabel.FontSize =
                11;

            statusLabel.FontWeight =
                FontWeights.Bold;

            statusLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#059669"));

            statusLabel.Margin =
                new Thickness(0, 5, 0, 6);

            statusLabel.HorizontalAlignment =
                HorizontalAlignment.Left;


            // =====================================================
            // درصد
            // =====================================================

            TextBlock percentLabel =
                new TextBlock();

            percentLabel.Text =
                "۰٪";

            percentLabel.FontSize =
                11;

            percentLabel.FontWeight =
                FontWeights.Bold;

            percentLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString(themeColor));

            percentLabel.HorizontalAlignment =
                HorizontalAlignment.Left;

            percentLabel.Margin =
                new Thickness(0, 0, 0, 4);


            // =====================================================
            // ProgressBar
            // =====================================================

            ProgressBar progressBar =
                new ProgressBar();

            progressBar.Minimum =
                0;

            progressBar.Maximum =
                1;

            progressBar.Value =
                0;

            progressBar.Height =
                8;

            progressBar.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E5E7EB"));

            progressBar.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#059669"));


            result.ScoreLabel =
                scoreLabel;

            result.DescriptionLabel =
                descriptionLabel;

            result.CurrentLabel =
                currentLabel;

            result.CapacityLabel =
                capacityLabel;

            result.RemainingLabel =
                remainingLabel;

            result.StatusLabel =
                statusLabel;

            result.PercentLabel =
                percentLabel;

            result.ProgressBar =
                progressBar;


            return result;
        }
        private Border CreateGuideScoreBorder(
            GuideScoreUI scoreUI,
            string themeColor)
        {
            Grid headerGrid =
                new Grid();


            StackPanel infoPanel =
                new StackPanel();


            infoPanel.Children.Add(
                scoreUI.ScoreLabel);

            infoPanel.Children.Add(
                scoreUI.DescriptionLabel);

            infoPanel.Children.Add(
                scoreUI.CurrentLabel);

            infoPanel.Children.Add(
                scoreUI.CapacityLabel);

            infoPanel.Children.Add(
                scoreUI.RemainingLabel);

            infoPanel.Children.Add(
                scoreUI.StatusLabel);

            infoPanel.Children.Add(
                scoreUI.PercentLabel);

            infoPanel.Children.Add(
                scoreUI.ProgressBar);


            Border border =
                new Border();

            border.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#F8FAFC"));

            border.BorderBrush =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E2E8F0"));

            border.BorderThickness =
                new Thickness(1);

            border.CornerRadius =
                new CornerRadius(11);

            border.Padding =
                new Thickness(13);

            border.Margin =
                new Thickness(2, 0, 2, 9);

            border.Child =
                infoPanel;


            return border;
        }
        private void CreateGuideCards()
        {
            if (GuideGrid == null)
                return;


            // =====================================================
            // اگر قبلاً ساخته شده‌اند
            // =====================================================

            if (isGuideUIInitialized &&
                guardGuideCard != null &&
                reserveGuideCard != null &&
                chiefGuideCard != null)
            {
                UpdateGuideCards();
                return;
            }


            GuideGrid.Children.Clear();


            // =====================================================
            // پاسدار
            // =====================================================

            guardGuideCard =
                CreateGuideCard(
                    "👮",
                    "افسر پاسدار",
                    "#2563EB");

            Grid.SetColumn(
                guardGuideCard.Card,
                0);

            GuideGrid.Children.Add(
                guardGuideCard.Card);


            // =====================================================
            // جانشین
            // =====================================================

            reserveGuideCard =
                CreateGuideCard(
                    "🔄",
                    "افسر جانشین",
                    "#059669");

            Grid.SetColumn(
                reserveGuideCard.Card,
                1);

            GuideGrid.Children.Add(
                reserveGuideCard.Card);


            // =====================================================
            // افسر سر
            // =====================================================

            chiefGuideCard =
                CreateGuideCard(
                    "⭐",
                    "افسر سر",
                    "#D97706");

            Grid.SetColumn(
                chiefGuideCard.Card,
                2);

            GuideGrid.Children.Add(
                chiefGuideCard.Card);


            isGuideUIInitialized =
                true;


            UpdateGuideCards();
        }
        private void UpdateGuideCards()
        {
            if (guardGuideCard == null ||
                reserveGuideCard == null ||
                chiefGuideCard == null)
            {
                return;
            }


            Dictionary<double, int> capacity =
                CalculateScoreCapacity();


            UpdateSingleGuideCard(
                guardGuideCard,
                guardOfficers,
                capacity);


            UpdateSingleGuideCard(
                reserveGuideCard,
                reserveOfficers,
                capacity);


            UpdateSingleGuideCard(
                chiefGuideCard,
                chiefOfficers,
                capacity);


            if (GuideProgramDaysLabel != null)
            {
                GuideProgramDaysLabel.Text =
                    selectedProgramDays.ToString() +
                    " روز";
            }
        }
        private void UpdateSingleGuideCard(
            GuideCardUI guideCard,
            List<Officer> officers,
            Dictionary<double, int> capacity)
        {
            foreach (double score in
                     new double[] { 0.5, 1.0, 2.0 })
            {
                GuideScoreUI scoreUI;

                if (!guideCard.Scores.TryGetValue(
                        score,
                        out scoreUI))
                {
                    continue;
                }


                int currentCount =
                    officers.Count(
                        delegate (Officer officer)
                        {
                            return officer.Score == score;
                        });


                int maxCapacity =
                    capacity.ContainsKey(score)
                        ? capacity[score]
                        : 0;


                int remaining =
                    maxCapacity -
                    currentCount;


                // ==========================================
                // تعداد فعلی
                // ==========================================

                scoreUI.CurrentLabel.Text =
                    "تعداد فعلی: " +
                    ToPersianNumber(currentCount) +
                    " نفر";


                // ==========================================
                // ظرفیت
                // ==========================================

                scoreUI.CapacityLabel.Text =
                    "ظرفیت: " +
                    ToPersianNumber(maxCapacity) +
                    " نفر";


                // ==========================================
                // باقی مانده
                // ==========================================

                if (remaining < 0)
                {
                    scoreUI.RemainingLabel.Text =
                        "مازاد: " +
                        ToPersianNumber(
                            Math.Abs(remaining)) +
                        " نفر";

                    scoreUI.RemainingLabel.Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media.ColorConverter
                                .ConvertFromString("#DC2626"));
                }
                else
                {
                    scoreUI.RemainingLabel.Text =
                        "ظرفیت باقی‌مانده: " +
                        ToPersianNumber(remaining) +
                        " نفر";

                    scoreUI.RemainingLabel.Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media.ColorConverter
                                .ConvertFromString("#059669"));
                }


                // ==========================================
                // وضعیت
                // ==========================================

                System.Windows.Media.Color statusColor;

                if (currentCount > maxCapacity)
                {
                    scoreUI.StatusLabel.Text =
                        "⚠ ظرفیت تکمیل شده و بیشتر از حد مجاز است";

                    statusColor =
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter
                            .ConvertFromString("#DC2626");
                }
                else if (currentCount == maxCapacity)
                {
                    scoreUI.StatusLabel.Text =
                        "⚠ ظرفیت تکمیل شده";

                    statusColor =
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter
                            .ConvertFromString("#D97706");
                }
                else
                {
                    scoreUI.StatusLabel.Text =
                        "✓ ظرفیت مجاز";

                    statusColor =
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter
                            .ConvertFromString("#059669");
                }


                scoreUI.StatusLabel.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        statusColor);


                // ==========================================
                // درصد
                // ==========================================

                double progress =
                    0;


                if (maxCapacity > 0)
                {
                    progress =
                        (double)currentCount /
                        (double)maxCapacity;


                    if (progress > 1)
                        progress = 1;
                }


                int percent =
                    (int)Math.Round(
                        progress * 100);


                scoreUI.PercentLabel.Text =
                    ToPersianNumber(percent) +
                    "٪";


                scoreUI.PercentLabel.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        statusColor);


                scoreUI.ProgressBar.Value =
                    progress;


                scoreUI.ProgressBar.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        statusColor);
            }
        }

        private string ToPersianNumber(
            int number)
        {
            return number
                .ToString()
                .Replace("0", "۰")
                .Replace("1", "۱")
                .Replace("2", "۲")
                .Replace("3", "۳")
                .Replace("4", "۴")
                .Replace("5", "۵")
                .Replace("6", "۶")
                .Replace("7", "۷")
                .Replace("8", "۸")
                .Replace("9", "۹");
        }
        private void GuideMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowGuide();
        }
        private void ShowGuide()
        {
            HideAllMainPages();

            GuideView.Visibility =
                Visibility.Visible;

            CreateGuideCards();

            UpdateGuideCards();
        }
        private void CreateScheduleDayUI(
    int day,
    Grid grid)
        {
            Border dayCard =
                new Border();

            dayCard.Background =
                System.Windows.Media.Brushes.White;

            dayCard.BorderBrush =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E2E8F0"));

            dayCard.BorderThickness =
                new Thickness(1);

            dayCard.CornerRadius =
                new CornerRadius(16);

            dayCard.Padding =
                new Thickness(14);

            dayCard.Margin =
                new Thickness(6, 6, 6, 6);


            StackPanel layout =
                new StackPanel();


            // ==============================
            // هدر
            // ==============================

            Grid header =
                new Grid();

            header.ColumnDefinitions.Add(
                new ColumnDefinition());

            header.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                });


            TextBlock dayLabel =
                new TextBlock();

            dayLabel.FontSize = 17;
            dayLabel.FontWeight =
                FontWeights.Bold;

            dayLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#0F172A"));

            Grid.SetColumn(
                dayLabel,
                0);


            TextBlock dateLabel =
                new TextBlock();

            dateLabel.FontSize = 11;

            dateLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#64748B"));

            dateLabel.TextAlignment =
                TextAlignment.Right;

            Grid.SetColumn(
                dateLabel,
                1);


            header.Children.Add(dayLabel);
            header.Children.Add(dateLabel);

            layout.Children.Add(header);


            Border separator =
                new Border();

            separator.Height = 1;

            separator.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E5E7EB"));

            separator.Margin =
                new Thickness(0, 12, 0, 10);

            layout.Children.Add(separator);


            // ==============================
            // تعطیلی
            // ==============================

            TextBlock holidayLabel =
                new TextBlock();

            holidayLabel.FontSize = 11;

            holidayLabel.FontWeight =
                FontWeights.Bold;

            holidayLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#DC2626"));

            holidayLabel.TextWrapping =
                TextWrapping.Wrap;

            holidayLabel.Visibility =
                Visibility.Collapsed;

            holidayLabel.Margin =
                new Thickness(0, 0, 0, 10);

            layout.Children.Add(
                holidayLabel);


            // ==============================
            // پاسها
            // ==============================

            Grid passesPanel =
                new Grid();

            passesPanel.Margin =
                new Thickness(0, 4, 0, 0);

            // سه ستون مساوی برای سه افسر
            for (int i = 0; i < 3; i++)
            {
                passesPanel.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                1,
                                GridUnitType.Star)
                    });
            }

            ScheduleDayUI dayUI =
                new ScheduleDayUI();

            dayUI.Card = dayCard;
            dayUI.DayLabel = dayLabel;
            dayUI.DateLabel = dateLabel;
            dayUI.HolidayLabel = holidayLabel;


            for (int pass = 1; pass <= 3; pass++)
            {
                SchedulePassUI passUI =
                    CreateSchedulePassUI(pass);

                Grid.SetColumn(
                    passUI.Card,
                    pass - 1);

                passesPanel.Children.Add(
                    passUI.Card);

                dayUI.Passes.Add(
                    passUI);
            }

            layout.Children.Add(
                passesPanel);

            dayCard.Child =
                layout;


            // ==============================
            // شبکه ۳ ستونه
            // ==============================

            int position =
                day - 1;

            int row =
                position / 3;

            int column =
                position % 3;


            while (grid.RowDefinitions.Count <= row)
            {
                grid.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    });
            }


            Grid.SetRow(
                dayCard,
                row);

            Grid.SetColumn(
                dayCard,
                column);


            grid.Children.Add(
                dayCard);


            scheduleDayCache[day] =
                dayUI;
        }
        private readonly Dictionary<DateTime, string> OfficialHolidays =
            new Dictionary<DateTime, string>
            {
                {
                    ToGregorianDate(1405, 5, 21),
                    "رحلت حضرت رسول اکرم (ص) و شهادت امام حسن مجتبی (ع)"
                },

                {
                    ToGregorianDate(1405, 5, 22),
                    "شهادت حضرت امام رضا (ع)"
                },

                {
                    ToGregorianDate(1405, 5, 30),
                    "شهادت حضرت امام حسن عسکری (ع)"
                },

                {
                    ToGregorianDate(1405, 6, 8),
                    "میلاد حضرت رسول اکرم (ص) و ولادت حضرت امام جعفر صادق (ع)"
                },

                {
                    ToGregorianDate(1405, 10, 2),
                    "ولادت حضرت امام علی (ع)"
                },

                {
                    ToGregorianDate(1405, 10, 16),
                    "مبعث حضرت رسول اکرم (ص)"
                },

                {
                    ToGregorianDate(1405, 11, 4),
                    "ولادت حضرت قائم (عج)"
                },

                {
                    ToGregorianDate(1405, 11, 22),
                    "پیروزی انقلاب اسلامی و سقوط نظام شاهنشاهی"
                },

                {
                    ToGregorianDate(1405, 12, 9),
                    "شهادت حضرت امیرالمؤمنین (ع)"
                },

                {
                    ToGregorianDate(1405, 12, 19),
                    "عید سعید فطر"
                },

                {
                    ToGregorianDate(1405, 12, 20),
                    "تعطیل به مناسبت عید سعید فطر"
                },

                {
                    ToGregorianDate(1405, 12, 29),
                    "آخرین روز سال و روز ملی شدن صنعت نفت ایران"
                }
                ,

                {
                    ToGregorianDate(1406, 1, 1),
                  "آغاز نوروز"
                }
                ,

                {
                    ToGregorianDate(1406, 1,2),
                    "عید نوروز"
                }
                ,

                {
                    ToGregorianDate(1406, 1, 3),
                    "عید نوروز"
                }
                ,

                {
                    ToGregorianDate(1406, 1, 4),
                    "عید نوروز"
                }
                ,

                {
                    ToGregorianDate(1406, 1, 12),
                    " روز جمهوری اسلامی ایران"
                }
                ,

                {
                    ToGregorianDate(1406, 1, 14),
                  "شهادت امام جعفر صادق علیه اسلام"
                }
                ,

                {
                    ToGregorianDate(1406, 2, 28),
                    "عید سعید قربان "
                }
                ,

                {
                    ToGregorianDate(1406, 3, 5),
                   "عید سعید غدیر"
                }
                ,

                {
                    ToGregorianDate(1406, 3, 15),
                   "قیام خونین 15 خرداد"
                }
              
                ,

                {
                    ToGregorianDate(1406, 3, 25),
                   "تاسوعای حسینی"
                }
                ,

                {
                    ToGregorianDate(1406, 3, 26),
                  "عاشورای حسینی"
                }
                ,

                {
                    ToGregorianDate(1406, 5, 4),
                   "اربعین حسینی"
                }
                ,

                {
                    ToGregorianDate(1406, 5, 12),
                  "شهادت حضرت امام حسن مجتبی علیه اسلام"
                }
                ,

                {
                    ToGregorianDate(1406, 5, 13),
                   "شهادت حضرت امام رضا علیه اسلام"
                }

                ,

                {
                    ToGregorianDate(1406, 5, 21),
                   "شهادت حضرت امام حسن عسکری علیه اسلام "
                }
                ,

                {
                    ToGregorianDate(1406, 5, 30),
                    "میلاد حضرت رسول اکرم صلی علیه  و آله "
                }
                ,

                {
                    ToGregorianDate(1406, 7, 13),
                    "شهادت حضرت فاطمه زهرا سلام الله علیها"
                }
                ,

                {
                    ToGregorianDate(1406, 8, 23),
                   "ولادت حضرت امام علی علیه اسلام و روز پدر"
                }
                ,

                {
                    ToGregorianDate(1406, 9, 7),
                    "مبعث حضرت رسول اکرم"
                }
                ,

                {
                    ToGregorianDate(1406, 12, 9),
                  "عید سعید فطر"
                }
                ,

                {
                    ToGregorianDate(1406, 12, 10),
                   "تعطیل به مناسبت عید سعید فطر "
                }
                ,

                {
                    ToGregorianDate(1406, 12, 29),
                   "آخرین روز سال و روز ملی شدن صنعت نفت"
                }
            };

        private static DateTime ToGregorianDate(
            int year,
            int month,
            int day)
        {
            PersianCalendar pc =
                new PersianCalendar();

            return pc.ToDateTime(
                year,
                month,
                day,
                0,
                0,
                0,
                0);
        }
        private bool IsOfficialHoliday(DateTime date)
        {
            DateTime normalizedDate =
                date.Date;

            return OfficialHolidays.ContainsKey(
                normalizedDate);
        }
        private string GetOfficialHolidayName(
            DateTime date)
        {
            DateTime normalizedDate =
                date.Date;

            string holidayName;

            if (OfficialHolidays.TryGetValue(
                    normalizedDate,
                    out holidayName))
            {
                return holidayName;
            }

            return "";
        }
        private SchedulePassUI CreateSchedulePassUI(
      int pass)
        {
            Border card =
                new Border();

            // =====================================================
            // اطلاعات سمت
            // =====================================================

            string accentColor;
            string icon;
            string roleText;

            if (pass == 1)
            {
                accentColor = "#059669";
                icon = "↻";
                roleText = "افسر جانشین";
            }
            else if (pass == 2)
            {
                accentColor = "#D97706";
                icon = "★";
                roleText = "افسر سر";
            }
            else
            {
                accentColor = "#2563EB";
                icon = "●";
                roleText = "افسر پاسدار";
            }


            // =====================================================
            // کارت بدون بک‌گراند
            // =====================================================

            // =====================================================
            // بخش هر افسر
            // =====================================================

            card.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#F8FAFC"));

            card.BorderBrush =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E2E8F0"));

            card.BorderThickness =
                new Thickness(1);

            card.CornerRadius =
                new CornerRadius(10);

            card.Padding =
                new Thickness(8, 7, 8, 7);

            card.Margin =
                new Thickness(3, 0, 3, 0);


            // =====================================================
            // محتوای افسر
            // =====================================================

            StackPanel panel =
                new StackPanel();

            panel.HorizontalAlignment =
                HorizontalAlignment.Stretch;


            // =====================================================
            // عنوان سمت
            // =====================================================

            TextBlock roleLabel =
                new TextBlock();

            roleLabel.Text =
                icon + "  " + roleText;

            roleLabel.FontSize =
                11;

            roleLabel.FontWeight =
                FontWeights.SemiBold;

            roleLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString(accentColor));

            roleLabel.HorizontalAlignment =
                HorizontalAlignment.Center;

            roleLabel.TextAlignment =
                TextAlignment.Center;


            // =====================================================
            // نام نیرو
            // =====================================================

            TextBlock personLabel =
                new TextBlock();

            personLabel.Text =
                "در انتظار انتخاب نیرو";

            personLabel.FontSize =
                14;

            personLabel.FontWeight =
                FontWeights.Bold;

            personLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#0F172A"));

            personLabel.HorizontalAlignment =
                HorizontalAlignment.Center;

            personLabel.TextAlignment =
                TextAlignment.Center;

            personLabel.TextWrapping =
                TextWrapping.Wrap;

            personLabel.Margin =
                new Thickness(0, 7, 0, 4);


            // =====================================================
            // امتیاز
            // =====================================================

            TextBlock scoreLabel =
                new TextBlock();

            scoreLabel.Text =
                "";

            scoreLabel.FontSize =
                10;

            scoreLabel.FontWeight =
                FontWeights.Normal;

            scoreLabel.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#64748B"));

            scoreLabel.HorizontalAlignment =
                HorizontalAlignment.Center;

            scoreLabel.TextAlignment =
                TextAlignment.Center;


            // =====================================================
            // اضافه کردن
            // =====================================================

            panel.Children.Add(
                roleLabel);

            panel.Children.Add(
                personLabel);

            panel.Children.Add(
                scoreLabel);


            card.Child =
                panel;


            return new SchedulePassUI
            {
                Card = card,
                PassLabel = roleLabel,
                PersonLabel = personLabel,
                ScoreLabel = scoreLabel
            };
        }
        private void MainWindow_Closing(
            object sender,
            System.ComponentModel.CancelEventArgs e)
        {
            SaveOfficerData();
            SaveSettings();
            SaveCurrentProgram();
        }
        private void ShowSchedule()
        {
            if (schedule == null ||
                schedule.Count == 0)
            {
                ScheduleContainer.Children.Clear();

                ScheduleStatusLabel.Text =
                    "هنوز برنامه‌ای ساخته نشده است.";

                return;
            }


            ScheduleContainer.Children.Clear();

            scheduleGrid =
                new Grid();

            scheduleGrid.FlowDirection =
                FlowDirection.RightToLeft;


            scheduleGrid.Margin =
                new Thickness(0);


            // سه ستون
            for (int i = 0; i < 3; i++)
            {
                scheduleGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    });
            }


            int totalDays =
                schedule
                    .Select(x => x.Day)
                    .DefaultIfEmpty(0)
                    .Max();


            scheduleDayCache.Clear();


            for (int day = 1;
                 day <= totalDays;
                 day++)
            {
                CreateScheduleDayUI(
                    day,
                    scheduleGrid);
            }


            ScheduleContainer.Children.Add(
                scheduleGrid);


            isScheduleUIInitialized =
                true;


            UpdateScheduleUI();


            ScheduleStatusLabel.Text =
                "برنامه با موفقیت ساخته شده است.";
        }
        private void UpdateScheduleUI()
        {
            if (scheduleGrid == null)
                return;

            if (schedule == null ||
                schedule.Count == 0)
                return;


            foreach (var dayGroup in
                     schedule
                         .GroupBy(x => x.Day)
                         .OrderBy(x => x.Key))
            {
                int day =
                    dayGroup.Key;


                ScheduleDayUI dayUI;

                if (!scheduleDayCache.TryGetValue(
                        day,
                        out dayUI))
                {
                    continue;
                }


                DateTime currentDate =
                    currentProgramStartDate.AddDays(
                        day - 1);


                // ==============================
                // روز
                // ==============================

                dayUI.DayLabel.Text =
                    "📅 روز " + day;


                // ==============================
                // تاریخ و روز هفته
                // ==============================

                dayUI.DateLabel.Text =
                    GetPersianWeekDay(
                        currentDate.DayOfWeek)
                    + "\n" +
                    GetPersianDate(
                        currentDate);


                // ==============================
                // تعطیلی
                // ==============================

                string holidayName =
                    GetOfficialHolidayName(
                        currentDate);


                if (!string.IsNullOrWhiteSpace(
                        holidayName))
                {
                    dayUI.HolidayLabel.Visibility =
                        Visibility.Visible;

                    dayUI.HolidayLabel.Text =
                        "🔴 تعطیل رسمی — " +
                        holidayName;
                }
                else
                {
                    dayUI.HolidayLabel.Visibility =
                        Visibility.Collapsed;

                    dayUI.HolidayLabel.Text =
                        "";
                }


                // ==============================
                // سه پاس
                // ==============================

                List<GuardScheduleItem> items =
                    dayGroup
                        .OrderBy(x => x.Pass)
                        .Take(3)
                        .ToList();


                for (int i = 0; i < 3; i++)
                {
                    if (i >= dayUI.Passes.Count)
                        continue;


                    SchedulePassUI passUI =
                        dayUI.Passes[i];


                    if (i < items.Count)
                    {
                        GuardScheduleItem item =
                            items[i];


                        if (item.Pass == 1)
                        {
                            passUI.PassLabel.Text =
                                "↻  افسر جانشین";
                        }
                        else if (item.Pass == 2)
                        {
                            passUI.PassLabel.Text =
                                "★  افسر سر";
                        }
                        else if (item.Pass == 3)
                        {
                            passUI.PassLabel.Text =
                                "●  افسر پاسدار";
                        }
                        else
                        {
                            passUI.PassLabel.Text =
                                "سمت نامشخص";
                        }


                        Officer officer =
                            GetScheduleOfficer(item);


                        if (officer != null)
                        {
                            passUI.PersonLabel.Text =
                                officer.Name;

                            passUI.ScoreLabel.Text =
                                "امتیاز: " +
                                officer.Score.ToString();
                        }
                        else
                        {
                            passUI.PersonLabel.Text =
                                "نیروی مناسب پیدا نشد";

                            passUI.ScoreLabel.Text =
                                "";
                        }

                        passUI.Card.Visibility =
                            Visibility.Visible;
                    }
                    else
                    {
                        passUI.Card.Visibility =
                            Visibility.Collapsed;
                    }
                }


                dayUI.Card.Visibility =
                    Visibility.Visible;
            }


            int totalDays =
                schedule
                    .Select(x => x.Day)
                    .DefaultIfEmpty(0)
                    .Max();

            if (ScheduleDaysInfoLabel != null)
            {
                ScheduleDaysInfoLabel.Text =
                    "تعداد روز: " +
                    totalDays.ToString();
            }

        }
        private Officer GetScheduleOfficer(
            GuardScheduleItem item)
        {
            if (item == null)
                return null;

            if (item.Pass == 1)
                return item.Reserve;

            if (item.Pass == 2)
                return item.Chief;

            if (item.Pass == 3)
                return item.Guard;

            return null;
        }
        private void CreateTestData_Click(
    object sender,
    RoutedEventArgs e)
        {
            try
            {
                // ==========================================
                // اگر اطلاعات تستی قبلاً ساخته شده:
                // کلیک دوم = حذف
                // ==========================================

                if (isTestDataActive)
                {
                    guardOfficers.Clear();
                    reserveOfficers.Clear();
                    chiefOfficers.Clear();

                    GuardOfficerContainer.Children.Clear();
                    ReserveOfficerContainer.Children.Clear();
                    ChiefOfficerContainer.Children.Clear();

                    ClearCurrentProgram();

                    isTestDataActive = false;

                    usedReplacementGuards.Clear();
                    usedReplacementReserves.Clear();
                    usedReplacementChiefs.Clear();

                    SaveOfficerData();
                    SaveSettings();

                    CreateTestDataButton.Content =
                        "🧪 ایجاد اطلاعات تستی";



                    RefreshOfficerLists();
                    UpdateDashboardStatistics();

                    MessageBox.Show(
                        "تمام اطلاعات تستی حذف شد.",
                        "اطلاعات تستی",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                // ==========================================
                // تعداد نیرو
                // ==========================================

                int testOfficerCount = 1;

                if (TestOfficerCountPicker.SelectedItem != null)
                {
                    ComboBoxItem item =
                        TestOfficerCountPicker.SelectedItem
                        as ComboBoxItem;

                    if (item != null &&
                        int.TryParse(
                            item.Content.ToString(),
                            out int selectedCount))
                    {
                        testOfficerCount = selectedCount;
                    }
                }

                // ==========================================
                // پاک کردن اطلاعات قبلی
                // ==========================================

                guardOfficers.Clear();
                reserveOfficers.Clear();
                chiefOfficers.Clear();

                GuardOfficerContainer.Children.Clear();
                ReserveOfficerContainer.Children.Clear();
                ChiefOfficerContainer.Children.Clear();

                schedule.Clear();

                // ==========================================
                // نام‌های تستی پاسدار
                // ==========================================

                string[] guardNames =
                {
            "علی احمدی",
            "رضا کریمی",
            "محمد حسینی",
            "حسن رضایی",
            "امیر محمدی",
            "مهدی اکبری",
            "سعید مرادی",
            "حامد نادری",
            "یاسر کاظمی",
            "نوید رستمی",
            "فرشاد رحیمی",
            "پیمان صادقی",
            "کامران شریفی",
            "میلاد نوری",
            "آرش موسوی",
            "بهرام یوسفی",
            "کیوان احمدپور",
            "سامان جعفری",
            "مجتبی کریمیان",
            "وحید نوروزی",
            "ایمان رجبی",
            "شهاب زمانی",
            "رضا نیکنام",
            "محسن اکبری"
        };

                // ==========================================
                // نام‌های تستی جانشین
                // ==========================================

                string[] reserveNames =
                {
            "احمد مرادی",
            "یوسف کریمی",
            "سجاد حسینی",
            "ناصر رضایی",
            "حسین محمدی",
            "فرهاد اکبری",
            "جواد مرادی",
            "کاظم نادری",
            "امین کاظمی",
            "بهنام رستمی",
            "داریوش رحیمی",
            "مسعود صادقی",
            "حسین شریفی",
            "مجید نوری",
            "فرزاد موسوی",
            "کوروش یوسفی",
            "مرتضی احمدپور",
            "مهران جعفری",
            "رضوان کریمیان",
            "حبیب نوروزی",
            "بابک رجبی",
            "سروش زمانی",
            "اکبر نیکنام",
            "احسان اکبری"
        };

                // ==========================================
                // نام‌های تستی افسر سر
                // ==========================================

                string[] chiefNames =
                {
            "پارسا احمدی",
            "مازیار کریمی",
            "آرمان حسینی",
            "شایان رضایی",
            "نیما محمدی",
            "کیارش اکبری",
            "آبتین مرادی",
            "بردیا نادری",
            "مانی کاظمی",
            "رامین رستمی",
            "بهنام رحیمی",
            "داریوش صادقی",
            "فرزاد شریفی",
            "یونس نوری",
            "کاوه موسوی",
            "بهنود یوسفی",
            "پویان احمدپور",
            "سینا جعفری",
            "نوید کریمیان",
            "بهنام نوروزی",
            "آرین رجبی",
            "مانی زمانی",
            "یاشار نیکنام",
            "رادین اکبری"
        };

                double[] scores =
                {
            0.5,
            1,
            2
        };

                // ==========================================
                // ساخت سه لیست
                // ==========================================

                for (int i = 0; i < testOfficerCount; i++)
                {
                    // -----------------------------
                    // پاسدار
                    // -----------------------------

                    Officer guard =
                        new Officer();

                    guard.Name =
                        guardNames[i];

                    guard.Score =
                        scores[i % 3];

                    guard.IsReplacement = false;

                    guardOfficers.Add(guard);

                    AddOfficerRow(
                        guard,
                        guardOfficers,
                        GuardOfficerContainer,
                        "#2563EB");

                    // -----------------------------
                    // جانشین
                    // -----------------------------

                    Officer reserve =
                        new Officer();

                    reserve.Name =
                        reserveNames[i];

                    reserve.Score =
                        scores[(i + 1) % 3];

                    reserve.IsReplacement = false;

                    reserveOfficers.Add(reserve);

                    AddOfficerRow(
                        reserve,
                        reserveOfficers,
                        ReserveOfficerContainer,
                        "#059669");


                    // -----------------------------
                    // افسر سر
                    // -----------------------------

                    Officer chief =
                        new Officer();

                    chief.Name =
                        chiefNames[i];

                    chief.Score =
                        scores[(i + 2) % 3];

                    chief.IsReplacement = false;

                    chiefOfficers.Add(chief);
                    AddOfficerRow(
                        chief,
                        chiefOfficers,
                        ChiefOfficerContainer,
                        "#D97706");
                }

                // ==========================================
                // وضعیت تست فعال
                // ==========================================

                isTestDataActive = true;

                CreateTestDataButton.Content =
                    "🗑 حذف اطلاعات تستی";

                SaveOfficerData();
                SaveSettings();
                SaveCurrentProgram();

                UpdateDashboardStatistics();

                MessageBox.Show(
                    "اطلاعات تستی با موفقیت ساخته شد.\r\n\r\n" +
                    "تعداد نیرو در هر لیست: " +
                    testOfficerCount,
                    "تست انجام شد",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "خطا در ایجاد اطلاعات تستی",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void ClearCurrentProgram()
        {
            try
            {
                schedule.Clear();

                currentProgramStartDate =
                    DateTime.Now.Date;

                scheduleGrid = null;

                scheduleDayCache.Clear();

                ScheduleContainer.Children.Clear();

                ScheduleStatusLabel.Text =
                    "هنوز برنامه‌ای ساخته نشده است.";

                if (ScheduleDaysInfoLabel != null)
                {
                    ScheduleDaysInfoLabel.Text =
                        "تعداد روز: " +
                        selectedProgramDays;
                }

                UpdateDashboardStatistics();


                string file =
                    GetCurrentProgramDataFile();

                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در پاک کردن برنامه فعلی:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void LoadSettings()
        {
            try
            {
                string file =
                    GetSettingsDataFile();

                AppSettings settings =
                    new AppSettings();

                if (File.Exists(file))
                {
                    XmlSerializer serializer =
                        new XmlSerializer(
                            typeof(AppSettings));

                    using (FileStream stream =
                           new FileStream(
                               file,
                               FileMode.Open,
                               FileAccess.Read))
                    {
                        settings =
                            (AppSettings)
                            serializer.Deserialize(stream);
                    }
                }


                // ==========================================
                // تعداد روز
                // ==========================================

                selectedProgramDays =
                    settings.ProgramDays;

                if (selectedProgramDays < 10 ||
                    selectedProgramDays > 31)
                {
                    selectedProgramDays = 31;
                }


                // ==========================================
                // جایگزین
                // ==========================================

                useReplacementOfficers =
                    settings.ReplacementEnabled;


                replacementCountPerGroup =
                    settings.ReplacementCount;

                if (replacementCountPerGroup < 1 ||
                    replacementCountPerGroup > 3)
                {
                    replacementCountPerGroup = 1;
                }


                // ==========================================
                // اعلان‌ها
                // ==========================================

                notificationsEnabled =
                    settings.NotificationsEnabled;


                // ==========================================
                // وضعیت اطلاعات تستی
                // ==========================================

                isTestDataActive =
                    settings.TestDataActive;


                // ==========================================
                // اعمال روی UI
                // ==========================================

                isLoadingSettings = true;


                SelectDayCount(
                    selectedProgramDays);


                ReplacementCheckBox.IsChecked =
                    useReplacementOfficers;


                ReplacementCountPicker.SelectedIndex =
                    replacementCountPerGroup - 1;


                ReplacementCountPicker.IsEnabled =
                    useReplacementOfficers;


                NotificationsEnabledCheckBox.IsChecked =
                    notificationsEnabled;


                if (isTestDataActive)
                {
                    CreateTestDataButton.Content =
                        "🗑 حذف اطلاعات تستی";
                }
                else
                {
                    CreateTestDataButton.Content =
                        "🧪 ایجاد اطلاعات تستی";
                }


                isLoadingSettings = false;


                UpdateOfficerDashboard();
                UpdateDashboardStatistics();
            }
            catch (Exception ex)
            {
                isLoadingSettings = false;

                MessageBox.Show(
                    "خطا در بارگذاری تنظیمات:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void AssignReplacementOfficers()
        {
            try
            {
                // ==========================================
                // حذف وضعیت جایگزین قبلی
                // ==========================================

                foreach (Officer officer in guardOfficers)
                    officer.IsReplacement = false;

                foreach (Officer officer in reserveOfficers)
                    officer.IsReplacement = false;

                foreach (Officer officer in chiefOfficers)
                    officer.IsReplacement = false;


                // ==========================================
                // اگر قابلیت جایگزین خاموش است
                // ==========================================

                if (!useReplacementOfficers)
                {
                    SaveOfficerData();
                    return;
                }


                // ==========================================
                // تعداد جایگزین در هر گروه
                // ==========================================

                int count =
                    replacementCountPerGroup;

                if (count < 1)
                    count = 1;

                if (count > 3)
                    count = 3;


                // ==========================================
                // تابع انتخاب چرخه‌ای
                // فقط امتیاز 2
                // ==========================================

                List<Officer> SelectFromCycle(
                    List<Officer> officers,
                    HashSet<string> usedHistory,
                    int number)
                {
                    List<Officer> candidates =
                        officers
                            .Where(x => x.Score == 2)
                            .ToList();


                    if (candidates.Count == 0)
                        return new List<Officer>();


                    // حذف افرادی که دیگر در لیست نیستند
                    usedHistory.RemoveWhere(
                        name =>
                            !candidates.Any(
                                x =>
                                    string.Equals(
                                        x.Name,
                                        name,
                                        StringComparison.OrdinalIgnoreCase)));


                    // نیروهایی که در چرخه فعلی انتخاب نشده‌اند
                    List<Officer> remaining =
                        candidates
                            .Where(
                                x =>
                                    !usedHistory.Contains(
                                        x.Name))
                            .ToList();


                    ShuffleList(remaining);


                    List<Officer> selected =
                        new List<Officer>();


                    // انتخاب از چرخه فعلی
                    int takeCount =
                        Math.Min(
                            number,
                            remaining.Count);


                    for (int i = 0;
                         i < takeCount;
                         i++)
                    {
                        selected.Add(
                            remaining[i]);
                    }


                    // ثبت در سابقه
                    foreach (Officer officer in selected)
                    {
                        usedHistory.Add(
                            officer.Name);
                    }


                    // ==========================================
                    // اگر به تعداد لازم نرسیدیم
                    // چرخه از اول شروع می‌شود
                    // ==========================================

                    if (selected.Count < number)
                    {
                        usedHistory.Clear();


                        List<Officer> newCycleCandidates =
                            candidates
                                .Where(
                                    x =>
                                        !selected.Contains(x))
                                .ToList();


                        ShuffleList(
                            newCycleCandidates);


                        int remainingCount =
                            number -
                            selected.Count;


                        List<Officer> newSelected =
                            newCycleCandidates
                                .Take(remainingCount)
                                .ToList();


                        selected.AddRange(
                            newSelected);


                        foreach (Officer officer in selected)
                        {
                            usedHistory.Add(
                                officer.Name);
                        }
                    }


                    return selected;
                }


                // ==========================================
                // گروه پاسدار
                // ==========================================

                List<Officer> guardReplacements =
                    SelectFromCycle(
                        guardOfficers,
                        usedReplacementGuards,
                        count);


                foreach (Officer officer
                         in guardReplacements)
                {
                    officer.IsReplacement =
                        true;
                }


                // ==========================================
                // گروه جانشین
                // ==========================================

                List<Officer> reserveReplacements =
                    SelectFromCycle(
                        reserveOfficers,
                        usedReplacementReserves,
                        count);


                foreach (Officer officer
                         in reserveReplacements)
                {
                    officer.IsReplacement =
                        true;
                }


                // ==========================================
                // گروه افسر سر
                // ==========================================

                List<Officer> chiefReplacements =
                    SelectFromCycle(
                        chiefOfficers,
                        usedReplacementChiefs,
                        count);


                foreach (Officer officer
                         in chiefReplacements)
                {
                    officer.IsReplacement =
                        true;
                }




                // ==========================================
                // ذخیره
                // ==========================================

                SaveOfficerData();
                RefreshOfficerLists();
                UpdateDashboardStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در انتخاب نیروهای جایگزین:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1;
                 i > 0;
                 i--)
            {
                int j =
                    replacementRandom.Next(
                        i + 1);

                T temp =
                    list[i];

                list[i] =
                    list[j];

                list[j] =
                    temp;
            }
        }
        private void CreateSchedule()
        {
            try
            {
                if (selectedProgramDays <= 0)
                {
                    MessageBox.Show(
                        "ابتدا تعداد روزهای برنامه را مشخص کنید.",
                        "خطا",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (guardOfficers.Count == 0 ||
                    reserveOfficers.Count == 0 ||
                    chiefOfficers.Count == 0)
                {
                    MessageBox.Show(
                        "هر سه لیست افسر پاسدار، جانشین و افسر سر باید دارای نیرو باشند.",
                        "خطا",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }
                // ==========================================
                // بررسی ظرفیت قبل از ایجاد برنامه
                // ==========================================
                if (!ValidateOfficerCapacity())
                {
                    return;
                }
                // ==========================================
                // مدیریت نیروهای جایگزین
                // ==========================================

                if (useReplacementOfficers)
                {
                    // انتخاب جایگزین‌ها از سه لیست
                    AssignReplacementOfficers();

                    // ساخت لیست مستقل جایگزین‌ها
                    UpdateReplacementOfficersList();
                }
                else
                {
                    foreach (Officer officer in guardOfficers)
                        officer.IsReplacement = false;

                    foreach (Officer officer in reserveOfficers)
                        officer.IsReplacement = false;

                    foreach (Officer officer in chiefOfficers)
                        officer.IsReplacement = false;

                    // خالی کردن لیست مستقل
                    replacementOfficers.Clear();

                    SaveOfficerData();
                }
                // ==========================================
                // انتخاب نیروهای جایگزین
                // قبل از ساخت برنامه
                // ==========================================




                currentProgramStartDate =
                    DateTime.Now.Date;

                schedule =
                    scheduleGenerator.Generate(
                        selectedProgramDays,
                        currentProgramStartDate,
                        guardOfficers,
                        reserveOfficers,
                        chiefOfficers);

                // ذخیره دائمی برنامه فعلی
                SaveCurrentProgram();

                // ذخیره Snapshot مستقل در تاریخچه
                SaveProgramHistorySnapshot();

                UpdateReports();
                ShowSchedule();
                ScheduleStatusLabel.Text =
                    "برنامه با موفقیت ساخته شد.\r\n" +
                    "تعداد روز: " + selectedProgramDays + "\r\n" +
                    "تعداد پاس: " + schedule.Count;

                ScheduleDaysInfoLabel.Text =
                    "تعداد روز: " + selectedProgramDays;

                UpdateDashboardStatistics();

                MessageBox.Show(
                    "برنامه نگهبانی با موفقیت ساخته شد.",
                    "موفق",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "خطا در ساخت برنامه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void UpdateDashboardStatistics()
        {
            int totalOfficers =
                guardOfficers.Count +
                reserveOfficers.Count +
                chiefOfficers.Count;

            int replacementCount =
                guardOfficers.Count(x => x.IsReplacement) +
                reserveOfficers.Count(x => x.IsReplacement) +
                chiefOfficers.Count(x => x.IsReplacement);

            if (DashboardDaysLabel != null)
                DashboardDaysLabel.Text =
                    selectedProgramDays.ToString();

            if (DashboardOfficersLabel != null)
                DashboardOfficersLabel.Text =
                    totalOfficers.ToString();

            if (DashboardReplacementLabel != null)
                DashboardReplacementLabel.Text =
                    replacementCount.ToString();

            if (DashboardPassCountLabel != null)
                DashboardPassCountLabel.Text =
                    schedule.Count.ToString();

            if (DashboardProgramStatusLabel != null)
            {
                DashboardProgramStatusLabel.Text =
                    schedule.Count > 0
                        ? "فعال"
                        : "بدون برنامه";
            }

            if (DashboardStatusLabel != null)
            {
                DashboardStatusLabel.Text =
                    schedule.Count > 0
                        ? "فعال"
                        : "آماده";
            }
        }
        private void CreateSchedule_Click(
            object sender,
            RoutedEventArgs e)
        {
            CreateSchedule();
        }
        private void SelectDayCount(int days)
        {
            string target = days.ToString();

            for (int i = 0;
                 i < DayCountPicker.Items.Count;
                 i++)
            {
                if (DayCountPicker.Items[i] != null &&
                    DayCountPicker.Items[i].ToString() == target)
                {
                    DayCountPicker.SelectedIndex = i;
                    return;
                }
            }

            // اگر پیدا نشد، 31 روز
            for (int i = 0;
                 i < DayCountPicker.Items.Count;
                 i++)
            {
                if (DayCountPicker.Items[i] != null &&
                    DayCountPicker.Items[i].ToString() == "31")
                {
                    DayCountPicker.SelectedIndex = i;
                    return;
                }
            }
        }

        private void DayCountPicker_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (isLoadingSettings)
                return;

            if (DayCountPicker.SelectedItem == null)
                return;

            int days;

            if (!int.TryParse(
                    DayCountPicker.SelectedItem.ToString(),
                    out days))
            {
                return;
            }

            selectedProgramDays = days;

            if (DashboardDaysLabel != null)
                DashboardDaysLabel.Text =
                    selectedProgramDays.ToString();

            if (ScheduleDaysInfoLabel != null)
                ScheduleDaysInfoLabel.Text =
                    "تعداد روز: " + selectedProgramDays;

            SaveSettings();
            UpdateGuideCards();
        }
        private void ReplacementCheckBox_Checked(
            object sender,
            RoutedEventArgs e)
        {
            if (isLoadingSettings)
                return;

            useReplacementOfficers = true;

            ReplacementCountPicker.IsEnabled =
                true;

            if (ReplacementCountPicker.SelectedIndex < 0)
            {
                ReplacementCountPicker.SelectedIndex = 0;
                replacementCountPerGroup = 1;
            }

            SaveSettings();

            UpdateOfficerDashboard();
        }
        private void ReplacementCheckBox_Unchecked(
            object sender,
            RoutedEventArgs e)
        {
            if (isLoadingSettings)
                return;

            useReplacementOfficers = false;

            ReplacementCountPicker.IsEnabled =
                false;

            foreach (Officer officer
                     in guardOfficers)
            {
                officer.IsReplacement = false;
            }

            foreach (Officer officer
                     in reserveOfficers)
            {
                officer.IsReplacement = false;
            }

            foreach (Officer officer
                     in chiefOfficers)
            {
                officer.IsReplacement = false;
            }
            replacementOfficers.Clear();
            if (ReplacementReportSection != null)
            {
                ReplacementReportSection.Visibility =
                    Visibility.Collapsed;
            }

            if (ReplacementReportGrid != null)
            {
                ReplacementReportGrid.ItemsSource =
                    null;
            }
            SaveOfficerData();
            SaveSettings();

            usedReplacementGuards.Clear();
            usedReplacementReserves.Clear();
            usedReplacementChiefs.Clear();

            UpdateOfficerDashboard();
        }
        private void ReplacementCountPicker_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (isLoadingSettings)
                return;

            if (ReplacementCountPicker.SelectedItem == null)
                return;

            ComboBoxItem item =
                ReplacementCountPicker.SelectedItem
                    as ComboBoxItem;

            if (item == null)
                return;

            int count;

            if (!int.TryParse(
                    item.Content.ToString(),
                    out count))
            {
                return;
            }

            replacementCountPerGroup =
                count;

            // با تغییر تعداد، چرخه از نو شروع شود
            usedReplacementGuards.Clear();
            usedReplacementReserves.Clear();
            usedReplacementChiefs.Clear();

            SaveSettings();
        }
        private void NotificationsEnabled_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (isLoadingSettings)
                return;

            notificationsEnabled =
                NotificationsEnabledCheckBox.IsChecked == true;

            SaveSettings();
        }
        private void ShowSettingsSection(string section)
        {
            // =====================================================
            // مخفی کردن همه بخش‌ها
            // =====================================================

            SettingsDaysSection.Visibility =
                Visibility.Collapsed;

            SettingsReplacementSection.Visibility =
                Visibility.Collapsed;

            SettingsHistorySection.Visibility =
                Visibility.Collapsed;

            SettingsPrioritySection.Visibility =
                Visibility.Collapsed;

            SettingsScoreSection.Visibility =
                Visibility.Collapsed;

            SettingsNotificationsSection.Visibility =
                Visibility.Collapsed;


            // =====================================================
            // رنگ پایه دکمه‌ها
            // =====================================================

            SettingsDaysButton.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E8F1FF"));

            SettingsDaysButton.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#1D4ED8"));

            SettingsReplacementButton.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E9F9F1"));

            SettingsReplacementButton.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#047857"));

            SettingsHistoryButton.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#F1EBFF"));

            SettingsHistoryButton.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#6D28D9"));

            SettingsPriorityButton.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#E9F8FA"));

            SettingsPriorityButton.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#0F766E"));

            SettingsScoreButton.Background =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#FFF7E6"));

            SettingsScoreButton.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter
                        .ConvertFromString("#B45309"));






            // =====================================================
            // فعال کردن بخش انتخاب شده
            // =====================================================

            if (section == "days")
            {
                SettingsDaysSection.Visibility =
                    Visibility.Visible;

                SettingsDaysButton.Background =
                    new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter
                            .ConvertFromString("#2563EB"));

                SettingsDaysButton.Foreground =
                    System.Windows.Media.Brushes.White;
            }
            else if (section == "replacement")
            {
                SettingsReplacementSection.Visibility =
                    Visibility.Visible;

                SettingsReplacementButton.Background =
                    new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter
                            .ConvertFromString("#059669"));

                SettingsReplacementButton.Foreground =
                    System.Windows.Media.Brushes.White;
            }
            else if (section == "history")
            {
                SettingsHistorySection.Visibility =
                    Visibility.Visible;

                SettingsHistoryButton.Background =
                    new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter
                            .ConvertFromString("#7C3AED"));

                SettingsHistoryButton.Foreground =
                    System.Windows.Media.Brushes.White;
            }
            else if (section == "priority")
            {
                SettingsPrioritySection.Visibility =
                    Visibility.Visible;

                SettingsPriorityButton.Background =
                    new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter
                            .ConvertFromString("#0F766E"));

                SettingsPriorityButton.Foreground =
                    System.Windows.Media.Brushes.White;
            }
            else if (section == "score")
            {
                SettingsScoreSection.Visibility =
                    Visibility.Visible;

                SettingsScoreButton.Background =
                    new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter
                            .ConvertFromString("#D97706"));

                SettingsScoreButton.Foreground =
                    System.Windows.Media.Brushes.White;
            }
            else if (section == "notifications")
            {
                SettingsNotificationsSection.Visibility =
                    Visibility.Visible;


            }
        }
        private void SettingsDaysButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettingsSection("days");
        }

        private void SettingsReplacementButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettingsSection("replacement");
        }

        private void SettingsHistoryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettingsSection("history");
        }

        private void SettingsPriorityButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettingsSection("priority");
        }

        private void SettingsScoreButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettingsSection("score");
        }

        private void SettingsNotificationsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettingsSection("notifications");
        }
        // =====================================================
        // مقداردهی اولیه
        // =====================================================
        private string GetDataFolder()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "HamedShahbazi");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return folder;
        }

        private string GetOfficerDataFile()
        {
            return Path.Combine(
                GetDataFolder(),
                "officers.xml");
        }
        private string GetSettingsDataFile()
        {
            return Path.Combine(
                GetDataFolder(),
                "settings.xml");
        }
        private string GetCurrentProgramDataFile()
        {
            return Path.Combine(
                GetDataFolder(),
                "current_program.xml");
        }
        private string GetProgramHistoryDataFile()
        {
            return Path.Combine(
                GetDataFolder(),
                "program_history.xml");
        }
        private void SaveProgramHistory()
        {
            try
            {
                ProgramHistoryStorage data =
                    new ProgramHistoryStorage();

                data.Programs =
                    new List<GuardProgramHistory>(
                        savedPrograms);

                XmlSerializer serializer =
                    new XmlSerializer(
                        typeof(ProgramHistoryStorage));

                using (FileStream stream =
                       new FileStream(
                           GetProgramHistoryDataFile(),
                           FileMode.Create,
                           FileAccess.Write))
                {
                    serializer.Serialize(
                        stream,
                        data);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ذخیره تاریخچه برنامه‌ها:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void LoadProgramHistory()
        {
            try
            {
                string file =
                    GetProgramHistoryDataFile();

                savedPrograms.Clear();

                if (!File.Exists(file))
                    return;

                XmlSerializer serializer =
                    new XmlSerializer(
                        typeof(ProgramHistoryStorage));

                ProgramHistoryStorage data;

                using (FileStream stream =
                       new FileStream(
                           file,
                           FileMode.Open,
                           FileAccess.Read))
                {
                    data =
                        (ProgramHistoryStorage)
                        serializer.Deserialize(stream);
                }

                if (data == null ||
                    data.Programs == null)
                {
                    return;
                }

                savedPrograms.AddRange(
                    data.Programs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در بارگذاری تاریخچه برنامه‌ها:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveProgramHistorySnapshot()
        {
            try
            {
                if (schedule == null ||
                    schedule.Count == 0)
                {
                    return;
                }

                int totalDays =
                    schedule
                        .Select(x => x.Day)
                        .DefaultIfEmpty(0)
                        .Max();

                if (totalDays <= 0)
                    totalDays = selectedProgramDays;

                int holidayCount = 0;

                for (int i = 0; i < totalDays; i++)
                {
                    DateTime day =
                        currentProgramStartDate.AddDays(i);

                    if (IsOfficialHoliday(day))
                        holidayCount++;
                }

                int replacementCount =
                    guardOfficers.Count(
                        x => x.IsReplacement)
                    +
                    reserveOfficers.Count(
                        x => x.IsReplacement)
                    +
                    chiefOfficers.Count(
                        x => x.IsReplacement);

                GuardProgramHistory history =
                    new GuardProgramHistory();

                history.Id =
                    Guid.NewGuid().ToString();

                history.CreatedAt =
                    DateTime.Now;

                history.StartDate =
                    currentProgramStartDate;

                history.ProgramDays =
                    totalDays;

                history.PassCount =
                    schedule.Count;

                history.HolidayCount =
                    holidayCount;

                history.ReplacementEnabled =
                    useReplacementOfficers;

                history.ReplacementCount =
                    replacementCount;

                history.Schedule =
                    new List<GuardScheduleItem>(
                        schedule);

                savedPrograms.Insert(
                    0,
                    history);

                SaveProgramHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ذخیره سابقه برنامه:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void SaveCurrentProgram()
        {
            try
            {
                CurrentProgramStorage data =
                    new CurrentProgramStorage();

                data.ProgramDays =
                    selectedProgramDays;

                data.StartDate =
                    currentProgramStartDate;

                if (schedule != null)
                {
                    data.Schedule =
                        new List<GuardScheduleItem>(
                            schedule);
                }

                data.UsedReplacementGuards =
                    usedReplacementGuards.ToList();

                data.UsedReplacementReserves =
                    usedReplacementReserves.ToList();

                data.UsedReplacementChiefs =
                    usedReplacementChiefs.ToList();


                XmlSerializer serializer =
                    new XmlSerializer(
                        typeof(CurrentProgramStorage));


                using (FileStream stream =
                       new FileStream(
                           GetCurrentProgramDataFile(),
                           FileMode.Create,
                           FileAccess.Write))
                {
                    serializer.Serialize(
                        stream,
                        data);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ذخیره برنامه نگهبانی:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void LoadCurrentProgram()
        {
            try
            {
                string file =
                    GetCurrentProgramDataFile();

                if (!File.Exists(file))
                    return;


                XmlSerializer serializer =
                    new XmlSerializer(
                        typeof(CurrentProgramStorage));


                CurrentProgramStorage data;


                using (FileStream stream =
                       new FileStream(
                           file,
                           FileMode.Open,
                           FileAccess.Read))
                {
                    data =
                        (CurrentProgramStorage)
                        serializer.Deserialize(stream);
                }


                if (data == null)
                    return;


                // ==========================================
                // تنظیمات برنامه
                // ==========================================

                if (data.ProgramDays >= 10 &&
                    data.ProgramDays <= 31)
                {
                    selectedProgramDays =
                        data.ProgramDays;

                    isLoadingSettings = true;

                    SelectDayCount(
                        selectedProgramDays);

                    isLoadingSettings = false;
                }


                // ==========================================
                // تاریخ شروع
                // ==========================================

                currentProgramStartDate =
                    data.StartDate.Date;


                // ==========================================
                // برنامه
                // ==========================================

                schedule.Clear();


                if (data.Schedule != null)
                {
                    schedule.AddRange(
                        data.Schedule);
                }


                // ==========================================
                // سابقه چرخه جایگزین
                // ==========================================

                usedReplacementGuards.Clear();
                usedReplacementReserves.Clear();
                usedReplacementChiefs.Clear();


                if (data.UsedReplacementGuards != null)
                {
                    foreach (string name
                             in data.UsedReplacementGuards)
                    {
                        usedReplacementGuards.Add(name);
                    }
                }


                if (data.UsedReplacementReserves != null)
                {
                    foreach (string name
                             in data.UsedReplacementReserves)
                    {
                        usedReplacementReserves.Add(name);
                    }
                }


                if (data.UsedReplacementChiefs != null)
                {
                    foreach (string name
                             in data.UsedReplacementChiefs)
                    {
                        usedReplacementChiefs.Add(name);
                    }
                }


                // ==========================================
                // بروزرسانی داشبورد
                // ==========================================

                UpdateDashboardStatistics();


                // ==========================================
                // بروزرسانی نمایش برنامه
                // ==========================================

                if (schedule.Count > 0)
                {
                    ShowSchedule();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در بارگذاری برنامه نگهبانی:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void LoadOfficerData()
        {
            try
            {
                string file = GetOfficerDataFile();

                guardOfficers.Clear();
                reserveOfficers.Clear();
                chiefOfficers.Clear();

                if (!File.Exists(file))
                {
                    RefreshOfficerLists();
                    UpdateOfficerDashboard();
                    return;
                }

                XmlSerializer serializer =
                    new XmlSerializer(typeof(OfficerStorage));

                OfficerStorage data;

                using (FileStream stream =
                       new FileStream(
                           file,
                           FileMode.Open,
                           FileAccess.Read))
                {
                    data =
                        (OfficerStorage)
                        serializer.Deserialize(stream);
                }

                if (data == null)
                {
                    RefreshOfficerLists();
                    UpdateOfficerDashboard();
                    return;
                }

                // ==========================
                // بازیابی لیست پاسدار
                // ==========================
                if (data.GuardOfficers != null)
                {
                    foreach (SavedOfficer saved in data.GuardOfficers)
                    {
                        Officer officer = new Officer();

                        officer.Name = saved.Name;
                        officer.Score = saved.Score;
                        officer.IsReplacement = saved.IsReplacement;

                        guardOfficers.Add(officer);
                    }
                }

                // ==========================
                // بازیابی لیست ذخیره
                // ==========================
                if (data.ReserveOfficers != null)
                {
                    foreach (SavedOfficer saved in data.ReserveOfficers)
                    {
                        Officer officer = new Officer();

                        officer.Name = saved.Name;
                        officer.Score = saved.Score;
                        officer.IsReplacement = saved.IsReplacement;

                        reserveOfficers.Add(officer);
                    }
                }

                // ==========================
                // بازیابی لیست افسر سر
                // ==========================
                if (data.ChiefOfficers != null)
                {
                    foreach (SavedOfficer saved in data.ChiefOfficers)
                    {
                        Officer officer = new Officer();

                        officer.Name = saved.Name;
                        officer.Score = saved.Score;
                        officer.IsReplacement = saved.IsReplacement;

                        chiefOfficers.Add(officer);
                    }
                }
                RefreshOfficerLists();

                UpdateReplacementOfficersList();

                UpdateOfficerDashboard();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در بارگذاری اطلاعات افسران:\n\n" +
                    ex.ToString(),
                    "خطا در بارگذاری",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateOfficerDashboard()
        {
            int total =
                guardOfficers.Count +
                reserveOfficers.Count +
                chiefOfficers.Count;

            int replacements =
                0;

            foreach (Officer officer in guardOfficers)
            {
                if (officer.IsReplacement)
                    replacements++;
            }

            foreach (Officer officer in reserveOfficers)
            {
                if (officer.IsReplacement)
                    replacements++;
            }

            foreach (Officer officer in chiefOfficers)
            {
                if (officer.IsReplacement)
                    replacements++;
            }

            DashboardOfficersLabel.Text =
                total.ToString();

            DashboardReplacementLabel.Text =
                replacements.ToString();
        }
        private void SaveOfficerData()
        {
            try
            {
                string file = GetOfficerDataFile();

                OfficerStorage data = new OfficerStorage();

                // ==========================
                // لیست افسران پاسدار
                // ==========================
                foreach (Officer officer in guardOfficers)
                {
                    data.GuardOfficers.Add(
                        new SavedOfficer
                        {
                            Name = officer.Name,
                            Score = officer.Score,
                            IsReplacement = officer.IsReplacement
                        });
                }

                // ==========================
                // لیست افسران ذخیره
                // ==========================
                foreach (Officer officer in reserveOfficers)
                {
                    data.ReserveOfficers.Add(
                        new SavedOfficer
                        {
                            Name = officer.Name,
                            Score = officer.Score,
                            IsReplacement = officer.IsReplacement
                        });
                }

                // ==========================
                // لیست افسران سر
                // ==========================
                foreach (Officer officer in chiefOfficers)
                {
                    data.ChiefOfficers.Add(
                        new SavedOfficer
                        {
                            Name = officer.Name,
                            Score = officer.Score,
                            IsReplacement = officer.IsReplacement
                        });
                }

                XmlSerializer serializer =
                    new XmlSerializer(typeof(OfficerStorage));

                using (FileStream stream =
                       new FileStream(
                           file,
                           FileMode.Create,
                           FileAccess.Write))
                {
                    serializer.Serialize(stream, data);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ذخیره اطلاعات افسران:\n\n" +
                    ex.ToString(),
                    "خطا در ذخیره",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddGuardOfficer_Click(
            object sender,
            RoutedEventArgs e)
        {
            string name =
                GuardOfficerEntry.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "لطفاً نام افسر پاسدار را وارد کنید.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (GuardOfficerScorePicker.SelectedItem == null)
            {
                MessageBox.Show(
                    "لطفاً امتیاز افسر پاسدار را انتخاب کنید.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            ComboBoxItem item =
                (ComboBoxItem)GuardOfficerScorePicker.SelectedItem;

            double score =
                Convert.ToDouble(
                    item.Content.ToString());

            Officer officer = new Officer();

            officer.Name = name;
            officer.Score = score;

            guardOfficers.Add(officer);

            SaveOfficerData();
            RefreshOfficerLists();
            UpdateOfficerDashboard();

            GuardOfficerEntry.Clear();
            GuardOfficerScorePicker.SelectedIndex = -1;

            MessageBox.Show(
                "افسر پاسدار با موفقیت اضافه شد.\n\n" +
                "نام: " + officer.Name + "\n" +
                "امتیاز: " + officer.Score.ToString(),
                "افزودن افسر",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        private void AddReserveOfficer_Click(
            object sender,
            RoutedEventArgs e)
        {
            string name =
                ReserveOfficerEntry.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "لطفاً نام افسر جانشین را وارد کنید.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (ReserveOfficerScorePicker.SelectedItem == null)
            {
                MessageBox.Show(
                    "لطفاً امتیاز افسر جانشین را انتخاب کنید.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            ComboBoxItem item =
                (ComboBoxItem)ReserveOfficerScorePicker.SelectedItem;

            double score =
                Convert.ToDouble(
                    item.Content.ToString());

            Officer officer = new Officer();

            officer.Name = name;
            officer.Score = score;

            reserveOfficers.Add(officer);

            SaveOfficerData();
            RefreshOfficerLists();
            UpdateOfficerDashboard();

            ReserveOfficerEntry.Clear();
            ReserveOfficerScorePicker.SelectedIndex = -1;

            MessageBox.Show(
                "افسر جانشین با موفقیت اضافه شد.\n\n" +
                "نام: " + officer.Name + "\n" +
                "امتیاز: " + officer.Score.ToString(),
                "افزودن افسر",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        private void AddChiefOfficer_Click(
            object sender,
            RoutedEventArgs e)
        {
            string name =
                ChiefOfficerEntry.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "لطفاً نام افسر سر را وارد کنید.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (ChiefOfficerScorePicker.SelectedItem == null)
            {
                MessageBox.Show(
                    "لطفاً امتیاز افسر سر را انتخاب کنید.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            ComboBoxItem item =
                (ComboBoxItem)ChiefOfficerScorePicker.SelectedItem;

            double score =
                Convert.ToDouble(
                    item.Content.ToString());

            Officer officer = new Officer();

            officer.Name = name;
            officer.Score = score;
            chiefOfficers.Add(officer);

            SaveOfficerData();
            RefreshOfficerLists();
            UpdateOfficerDashboard();

            ChiefOfficerEntry.Clear();
            ChiefOfficerScorePicker.SelectedIndex = -1;

            MessageBox.Show(
                "افسر سر با موفقیت اضافه شد.\n\n" +
                "نام: " + officer.Name + "\n" +
                "امتیاز: " + officer.Score.ToString(),
                "افزودن افسر",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        private void RefreshOfficerLists()
        {
            GuardOfficerContainer.Children.Clear();
            ReserveOfficerContainer.Children.Clear();
            ChiefOfficerContainer.Children.Clear();

            foreach (Officer officer in guardOfficers)
            {
                AddOfficerRow(
                    officer,
                    guardOfficers,
                    GuardOfficerContainer,
                    "#2563EB");
            }

            foreach (Officer officer in reserveOfficers)
            {
                AddOfficerRow(
                    officer,
                    reserveOfficers,
                    ReserveOfficerContainer,
                    "#059669");
            }

            foreach (Officer officer in chiefOfficers)
            {
                AddOfficerRow(
                    officer,
                    chiefOfficers,
                    ChiefOfficerContainer,
                    "#D97706");
            }
        }

        // =====================================================
        // آکاردئون مدیریت نیروها
        // =====================================================

        private void OfficerAccordionHeader_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button clickedButton =
                sender as Button;

            if (clickedButton == null)
                return;

            string section =
                clickedButton.Tag == null
                    ? ""
                    : clickedButton.Tag.ToString();


            bool guardWasOpen =
                GuardAccordionContent.Visibility ==
                Visibility.Visible;

            bool reserveWasOpen =
                ReserveAccordionContent.Visibility ==
                Visibility.Visible;

            bool chiefWasOpen =
                ChiefAccordionContent.Visibility ==
                Visibility.Visible;


            // =====================================================
            // همه بسته شوند
            // =====================================================

            GuardAccordionContent.Visibility =
                Visibility.Collapsed;

            ReserveAccordionContent.Visibility =
                Visibility.Collapsed;

            ChiefAccordionContent.Visibility =
                Visibility.Collapsed;


            GuardAccordionArrow.Text =
                "▶";

            ReserveAccordionArrow.Text =
                "▶";

            ChiefAccordionArrow.Text =
                "▶";


            // =====================================================
            // باز / بسته کردن مورد انتخاب‌شده
            // =====================================================

            if (section == "guard")
            {
                if (!guardWasOpen)
                {
                    GuardAccordionContent.Visibility =
                        Visibility.Visible;

                    GuardAccordionArrow.Text =
                        "▼";
                }
            }

            else if (section == "reserve")
            {
                if (!reserveWasOpen)
                {
                    ReserveAccordionContent.Visibility =
                        Visibility.Visible;

                    ReserveAccordionArrow.Text =
                        "▼";
                }
            }

            else if (section == "chief")
            {
                if (!chiefWasOpen)
                {
                    ChiefAccordionContent.Visibility =
                        Visibility.Visible;

                    ChiefAccordionArrow.Text =
                        "▼";
                }
            }
        }
        private void AddOfficerRow(
         Officer officer,
         List<Officer> sourceList,
         Panel container,
         string borderColor)
        {
            Border border = new Border();

            border.Background =
                System.Windows.Media.Brushes.White;
            border.MouseEnter += delegate
            {
                border.Background =
                    new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)
                        System.Windows.Media.ColorConverter.ConvertFromString(
                            "#F3F6FA"));
            };

            border.MouseLeave += delegate
            {
                border.Background =
                    System.Windows.Media.Brushes.White;
            };
            border.BorderBrush =
                new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)
                    System.Windows.Media.ColorConverter.ConvertFromString(
                        "#D1D5DB"));

            border.BorderThickness =
                new Thickness(1);

            border.CornerRadius =
                new CornerRadius(10);

            border.Margin =
                new Thickness(0, 0, 0, 8);

            Grid grid = new Grid();

            // خیلی مهم:
            // ترتیب ستون‌ها از چپ به راست:
            // 0 = شماره
            // 1 = اسم
            // 2 = امتیاز
            // 3 = فضای خالی
            // 4 = ویرایش
            // 5 = حذف
            grid.FlowDirection =
                FlowDirection.RightToLeft;

            // شماره
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                });

            // اسم
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                });

            // امتیاز
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                });

            // فضای خالی
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(
                        1,
                        GridUnitType.Star)
                });

            // ویرایش
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                });

            // حذف
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                });

            int number =
                sourceList.IndexOf(officer) + 1;

            // =========================================
            // شماره
            // =========================================
            TextBlock numberLabel =
                new TextBlock();

            numberLabel.Text =
                number.ToString();

            numberLabel.FontSize = 15;

            numberLabel.Foreground =
                System.Windows.Media.Brushes.Black;

            numberLabel.FlowDirection =
                FlowDirection.LeftToRight;

            numberLabel.VerticalAlignment =
                VerticalAlignment.Center;

            numberLabel.HorizontalAlignment =
                HorizontalAlignment.Center;

            numberLabel.Margin =
                new Thickness(5, 8, 5, 8);

            numberLabel.TextAlignment =
                TextAlignment.Center;

            Grid.SetColumn(
                numberLabel,
                0);

            // =========================================
            // اسم افسر
            // =========================================
            TextBlock nameLabel =
                new TextBlock();

            nameLabel.Text =
                officer.Name;

            nameLabel.FontSize = 15;

            nameLabel.Foreground =
                System.Windows.Media.Brushes.Black;

            nameLabel.FlowDirection =
                FlowDirection.RightToLeft;

            nameLabel.VerticalAlignment =
                VerticalAlignment.Center;

            nameLabel.HorizontalAlignment =
                HorizontalAlignment.Left;

            nameLabel.Margin =
                new Thickness(8, 8, 8, 8);

            nameLabel.TextAlignment =
                TextAlignment.Right;

            Grid.SetColumn(
                nameLabel,
                1);

            // =========================================
            // امتیاز
            // =========================================
            TextBlock scoreLabel =
                new TextBlock();

            scoreLabel.Text =
                "امتیاز: " +
                officer.Score.ToString();

            scoreLabel.FontSize = 14;

            scoreLabel.Foreground =
                System.Windows.Media.Brushes.Black;

            scoreLabel.FlowDirection =
                FlowDirection.RightToLeft;

            scoreLabel.VerticalAlignment =
                VerticalAlignment.Center;

            scoreLabel.HorizontalAlignment =
                HorizontalAlignment.Center;

            scoreLabel.Margin =
                new Thickness(8, 8, 8, 8);

            scoreLabel.TextAlignment =
                TextAlignment.Center;

            Grid.SetColumn(
                scoreLabel,
                2);

            // =========================================
            // دکمه ویرایش
            // =========================================
            Button editButton =
                new Button();

            editButton.Content =
                new System.Windows.Controls.Image
                {
                    Width = 25,
                    Height = 25,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Source =
                        new System.Windows.Media.Imaging.BitmapImage(
                            new Uri(
                                "pack://application:,,,/HamedShahbazi.Win7;component/Resources/icons8-edit-96.png",
                                UriKind.Absolute))
                };
            editButton.Width = 42;
            editButton.Height = 36;

            editButton.Margin =
                new Thickness(5);

            editButton.Template =
                CreateNoHoverButtonTemplate();

            editButton.Click +=
                delegate
                {
                    EditOfficer(
                        officer,
                        sourceList);
                };

            Grid.SetColumn(
                editButton,
                4);

            // =========================================
            // دکمه حذف
            // =========================================
            Button deleteButton =
                new Button();

            deleteButton.Content =
                new System.Windows.Controls.Image
                {
                    Width = 20,
                    Height = 20,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Source =
                        new System.Windows.Media.Imaging.BitmapImage(
                            new Uri(
                                "pack://application:,,,/HamedShahbazi.Win7;component/Resources/icons8-delete-96.png",
                                UriKind.Absolute))
                };
            deleteButton.Width = 42;
            deleteButton.Height = 36;

            deleteButton.Margin =
                new Thickness(5);

            deleteButton.Template =
                CreateNoHoverButtonTemplate();

            deleteButton.Click +=
                delegate
                {
                    DeleteOfficer(
                        officer,
                        sourceList);
                };

            Grid.SetColumn(
                deleteButton,
                5);

            // =========================================
            // اضافه کردن همه کنترل‌ها به Grid
            // =========================================
            grid.Children.Add(numberLabel);
            grid.Children.Add(nameLabel);
            grid.Children.Add(scoreLabel);
            grid.Children.Add(editButton);
            grid.Children.Add(deleteButton);

            border.Child = grid;

            container.Children.Add(border);
        }
        private void DeleteOfficer(
            Officer officer,
            List<Officer> sourceList)
        {
            if (officer == null)
                return;

            MessageBoxResult result =
                MessageBox.Show(
                    "آیا از حذف این افسر مطمئن هستید؟\n\n" +
                    officer.Name,
                    "حذف افسر",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            sourceList.Remove(officer);

            SaveOfficerData();
            RefreshOfficerLists();
            UpdateOfficerDashboard();
        }
        private void EditOfficer(
            Officer officer,
            List<Officer> sourceList)
        {
            OfficerEditWindow window =
                new OfficerEditWindow(
                    officer.Name,
                    officer.Score);

            window.Owner = this;

            bool? result =
                window.ShowDialog();

            if (result == true)
            {
                officer.Name =
                    window.OfficerName;

                officer.Score =
                    window.OfficerScore;

                SaveOfficerData();
                RefreshOfficerLists();
                UpdateOfficerDashboard();
            }
        }
        private void InitializeDashboard()
        {
            PersianCalendar pc = new PersianCalendar();

            DateTime now = DateTime.Now;

            TodayDateLabel.Text =
                string.Format(
                    "{0:0000}/{1:00}/{2:00}",
                    pc.GetYear(now),
                    pc.GetMonth(now),
                    pc.GetDayOfMonth(now));

            TodayDayLabel.Text =
                GetPersianWeekDay(now.DayOfWeek);

            DashboardStatusLabel.Text = "فعال";
            DashboardProgramStatusLabel.Text = "فعال";

            DashboardDaysLabel.Text = "31";
            DashboardOfficersLabel.Text = "0";
            DashboardReplacementLabel.Text = "0";
            DashboardPassCountLabel.Text = "0";
            DashboardHolidayCountLabel.Text = "0";
            DashboardSavedProgramsLabel.Text = "0";
            DashboardLastProgramDateLabel.Text = "—";
        }

        // =====================================================
        // تعداد روزهای برنامه
        // =====================================================

        private void InitializeDayCount()
        {
            DayCountPicker.Items.Clear();

            for (int i = 10; i <= 31; i++)
            {
                DayCountPicker.Items.Add(i.ToString());
            }

            DayCountPicker.SelectedItem = "31";

            ReplacementCountPicker.SelectedIndex = 0;
        }

        // =====================================================
        // شروع برنامه
        // =====================================================
        // =====================================================
        // خروجی PDF برنامه نگهبانی
        // =====================================================

        private void SchedulePdfButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (schedule == null ||
                    schedule.Count == 0)
                {
                    MessageBox.Show(
                        "ابتدا برنامه نگهبانی را ایجاد کنید.",
                        "خروجی PDF",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                SaveFileDialog dialog =
                    new SaveFileDialog();

                dialog.Title =
                    "ذخیره PDF برنامه نگهبانی";

                dialog.Filter =
                    "PDF Files (*.pdf)|*.pdf";

                dialog.FileName =
                    "برنامه نگهبانی_" +
                    GetPersianDate(DateTime.Now)
                        .Replace("/", "-") +
                    ".pdf";

                bool? result =
                    dialog.ShowDialog();

                if (result != true)
                    return;

                CreateSchedulePdf(
                    dialog.FileName);

                MessageBox.Show(
                    "PDF برنامه نگهبانی با موفقیت ایجاد شد.\n\n" +
                    dialog.FileName,
                    "خروجی PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ایجاد PDF برنامه نگهبانی:\n\n" +
                    ex.ToString(),
                    "خطای PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        // =====================================================
        // ساخت PDF برنامه نگهبانی
        // حداکثر 4 کارت در هر صفحه
        // =====================================================

        private void CreateSchedulePdf(
            string fileName)
        {
            CreateSchedulePdf(
                fileName,
                schedule,
                currentProgramStartDate,
                selectedProgramDays);
        }
        private void CreateSchedulePdf(
    string fileName,
    List<GuardScheduleItem> sourceSchedule,
    DateTime startDate,
    int programDays)
        {
            Document document =
                new Document(
                    PageSize.A4,
                    28,
                    28,
                    24,
                    24);

            PdfWriter writer =
                PdfWriter.GetInstance(
                    document,
                    new FileStream(
                        fileName,
                        FileMode.Create,
                        FileAccess.Write));

            writer.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            document.Open();

            BaseFont baseFont =
                GetPdfBaseFont();

            iTextSharp.text.Font titleFont =
                new iTextSharp.text.Font(
                    baseFont,
                    19,
                    iTextSharp.text.Font.BOLD,
                    BaseColor.BLACK);

            iTextSharp.text.Font headerFont =
                new iTextSharp.text.Font(
                    baseFont,
                    10,
                    iTextSharp.text.Font.NORMAL,
                    new BaseColor(70, 70, 70));

            iTextSharp.text.Font dayFont =
                new iTextSharp.text.Font(
                    baseFont,
                    16,
                    iTextSharp.text.Font.BOLD,
                    BaseColor.BLACK);

            iTextSharp.text.Font normalFont =
                new iTextSharp.text.Font(
                    baseFont,
                    10,
                    iTextSharp.text.Font.NORMAL,
                    BaseColor.BLACK);

            iTextSharp.text.Font officerFont =
                new iTextSharp.text.Font(
                    baseFont,
                    10,
                    iTextSharp.text.Font.NORMAL,
                    BaseColor.BLACK);

            iTextSharp.text.Font roleFont =
                new iTextSharp.text.Font(
                    baseFont,
                    10,
                    iTextSharp.text.Font.BOLD,
                    BaseColor.BLACK);

            if (sourceSchedule == null)
                sourceSchedule =
                    new List<GuardScheduleItem>();

            int totalDays =
                sourceSchedule
                    .Select(x => x.Day)
                    .DefaultIfEmpty(0)
                    .Max();

            if (totalDays <= 0)
                totalDays = programDays;

            if (totalDays <= 0)
                totalDays = 1;

            // =====================================================
            // هر صفحه حداکثر 4 روز
            // =====================================================

            for (int startDay = 1;
                 startDay <= totalDays;
                 startDay += 4)
            {
                if (startDay > 1)
                {
                    document.NewPage();
                }

                // =================================================
                // HEADER اصلی PDF
                // =================================================

                PdfPTable mainHeader =
                    new PdfPTable(1);

                mainHeader.WidthPercentage =
                    100;

                mainHeader.RunDirection =
                    PdfWriter.RUN_DIRECTION_RTL;

                mainHeader.HorizontalAlignment =
                    Element.ALIGN_CENTER;

                // عنوان
                PdfPCell titleCell =
                    new PdfPCell();

                titleCell.Border =
                    iTextSharp.text.Rectangle.NO_BORDER;

                titleCell.Padding =
                    0;

                titleCell.PaddingBottom =
                    4;

                titleCell.RunDirection =
                    PdfWriter.RUN_DIRECTION_RTL;

                Paragraph titleParagraph =
                    new Paragraph(
                        "برنامه نگهبانی",
                        titleFont);

                titleParagraph.Alignment =
                    Element.ALIGN_CENTER;

                titleCell.AddElement(
                    titleParagraph);

                mainHeader.AddCell(
                    titleCell);

                // تاریخ ایجاد
                PdfPCell createDateCell =
                    new PdfPCell();

                createDateCell.Border =
                    iTextSharp.text.Rectangle.NO_BORDER;

                createDateCell.Padding =
                    0;

                createDateCell.RunDirection =
                    PdfWriter.RUN_DIRECTION_RTL;

                Paragraph createDateParagraph =
                    new Paragraph(
                        "تاریخ ایجاد برنامه: " +
                        GetPersianWeekDay(
                            startDate.DayOfWeek) +
                        "  " +
                        GetPersianDateForPdf(
                            startDate),
                        headerFont);

                createDateParagraph.Alignment =
                    Element.ALIGN_CENTER;

                createDateCell.AddElement(
                    createDateParagraph);

                mainHeader.AddCell(
                    createDateCell);

                // تاریخ چاپ
                PdfPCell printDateCell =
                    new PdfPCell();

                printDateCell.Border =
                    iTextSharp.text.Rectangle.NO_BORDER;

                printDateCell.Padding =
                    0;

                printDateCell.RunDirection =
                    PdfWriter.RUN_DIRECTION_RTL;

                Paragraph printDateParagraph =
                    new Paragraph(
                        "تاریخ چاپ: " +
                        GetPersianWeekDay(
                            DateTime.Now.DayOfWeek) +
                        "  " +
                        GetPersianDateForPdf(
                            DateTime.Now),
                        headerFont);

                printDateParagraph.Alignment =
                    Element.ALIGN_CENTER;

                printDateCell.AddElement(
                    printDateParagraph);

                mainHeader.AddCell(
                    printDateCell);

                // تعداد روز
                PdfPCell daysCountCell =
                    new PdfPCell();

                daysCountCell.Border =
                    iTextSharp.text.Rectangle.NO_BORDER;

                daysCountCell.Padding =
                    0;

                daysCountCell.PaddingBottom =
                    8;

                daysCountCell.RunDirection =
                    PdfWriter.RUN_DIRECTION_RTL;

                Paragraph daysParagraph =
                    new Paragraph(
                        "تعداد روز برنامه: " +
                        totalDays.ToString(),
                        headerFont);

                daysParagraph.Alignment =
                    Element.ALIGN_CENTER;

                daysCountCell.AddElement(
                    daysParagraph);

                mainHeader.AddCell(
                    daysCountCell);

                document.Add(
                    mainHeader);

                document.Add(
                    new Paragraph(" "));

                PdfPTable daysTable =
                    new PdfPTable(1);

                daysTable.WidthPercentage =
                    100;

                daysTable.RunDirection =
                    PdfWriter.RUN_DIRECTION_RTL;

                daysTable.HorizontalAlignment =
                    Element.ALIGN_CENTER;

                daysTable.SplitRows =
                    false;

                daysTable.SplitLate =
                    true;

                // =================================================
                // حداکثر 4 کارت
                // =================================================

                for (int offset = 0;
                     offset < 4;
                     offset++)
                {
                    int day =
                        startDay + offset;

                    if (day > totalDays)
                        break;

                    PdfPTable dayCard =
                        CreateSchedulePdfDayCard(
                            day,
                            sourceSchedule,
                            startDate,
                            baseFont,
                            dayFont,
                            normalFont,
                            officerFont,
                            roleFont);

                    PdfPCell cardWrapper =
                        new PdfPCell();

                    cardWrapper.Border =
                        iTextSharp.text.Rectangle.NO_BORDER;

                    cardWrapper.Padding =
                        0;

                    cardWrapper.PaddingBottom =
                        10;

                    cardWrapper.RunDirection =
                        PdfWriter.RUN_DIRECTION_RTL;

                    cardWrapper.AddElement(
                        dayCard);

                    daysTable.AddCell(
                        cardWrapper);
                }

                document.Add(
                    daysTable);
            }

            document.Close();
            writer.Close();
        }
        // =====================================================
        // فونت فارسی PDF
        // =====================================================
        // =====================================================
        // کارت دقیق یک روز برنامه
        // =====================================================

        private PdfPTable CreateSchedulePdfDayCard(
            int day,
            List<GuardScheduleItem> sourceSchedule,
            DateTime startDate,
            BaseFont baseFont,
            iTextSharp.text.Font dayFont,
            iTextSharp.text.Font normalFont,
            iTextSharp.text.Font officerFont,
            iTextSharp.text.Font roleFont)
        {
            DateTime currentDate =
                startDate.AddDays(
                    day - 1);

            // =====================================================
            // کارت اصلی
            // =====================================================

            PdfPTable card =
                new PdfPTable(1);

            card.WidthPercentage =
                100;

            card.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            card.HorizontalAlignment =
                Element.ALIGN_CENTER;

            card.SplitRows =
                false;

            card.SplitLate =
                true;

            // =====================================================
            // Header خاکستری کارت
            // =====================================================

            PdfPCell headerCell =
                new PdfPCell();

            headerCell.BackgroundColor =
                new BaseColor(
                    245,
                    245,
                    245);

            headerCell.Border =
                iTextSharp.text.Rectangle.BOX;

            headerCell.BorderColor =
                new BaseColor(
                    190,
                    190,
                    190);

            headerCell.BorderWidth =
                1;

            headerCell.Padding =
                8;

            headerCell.PaddingTop =
                9;

            headerCell.PaddingBottom =
                9;

            headerCell.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            // -----------------------------
            // روز
            // -----------------------------

            Paragraph dayParagraph =
                new Paragraph();

            dayParagraph.Alignment =
                Element.ALIGN_LEFT;

            Chunk dayChunk =
                new Chunk(
                    "روز " +
                    day.ToString(),
                    dayFont);

            dayParagraph.Add(
                dayChunk);

            // -----------------------------
            // روز هفته
            // -----------------------------

            Paragraph weekParagraph =
                new Paragraph(
                    GetPersianWeekDay(
                        currentDate.DayOfWeek),
                    normalFont);

            weekParagraph.Alignment =
                Element.ALIGN_LEFT;
            weekParagraph.SpacingBefore =
                1;

            weekParagraph.SpacingAfter =
                1;

            // -----------------------------
            // تاریخ
            // -----------------------------

            Paragraph dateParagraph =
                new Paragraph();

            dateParagraph.Alignment =
                Element.ALIGN_LEFT;

            Chunk calendarChunk =
                new Chunk(
                    "▣ ",
                    normalFont);

            Chunk dateChunk =
                new Chunk(
                    GetPersianDate(
                        currentDate),
                    normalFont);

            dateParagraph.Add(
                calendarChunk);

            dateParagraph.Add(
                dateChunk);

            // -----------------------------
            // اضافه به هدر
            // -----------------------------

            headerCell.AddElement(
                dayParagraph);

            headerCell.AddElement(
                weekParagraph);

            headerCell.AddElement(
                dateParagraph);

            // -----------------------------
            // اضافه کردن Header به کارت
            // -----------------------------

            card.AddCell(
                headerCell);

            // =====================================================
            // تعطیلی رسمی
            // =====================================================

            string holidayName =
                GetOfficialHolidayName(
                    currentDate);

            if (!string.IsNullOrWhiteSpace(
                    holidayName))
            {
                PdfPCell holidayCell =
                    new PdfPCell();

                holidayCell.BackgroundColor =
                    new BaseColor(
                        255,
                        248,
                        248);

                holidayCell.Border =
                    iTextSharp.text.Rectangle.LEFT_BORDER |
                    iTextSharp.text.Rectangle.RIGHT_BORDER |
                    iTextSharp.text.Rectangle.BOTTOM_BORDER;

                holidayCell.BorderColor =
                    new BaseColor(
                        210,
                        190,
                        190);

                holidayCell.Padding =
                    6;

                holidayCell.RunDirection =
                    PdfWriter.RUN_DIRECTION_RTL;

                Paragraph holidayParagraph =
                    new Paragraph(
                        "تعطیل رسمی — " +
                        holidayName,
                        normalFont);

                holidayParagraph.Alignment =
                    Element.ALIGN_RIGHT;

                holidayCell.AddElement(
                    holidayParagraph);

                card.AddCell(
                    holidayCell);
            }

            // =====================================================
            // پیدا کردن سه پاس
            // =====================================================

            List<GuardScheduleItem> items =
                sourceSchedule
                    .Where(
                           delegate (GuardScheduleItem item)
                           {
                               return item.Day == day;
                           })
                    .OrderBy(
                        delegate (GuardScheduleItem item)
                        {
                            return item.Pass;
                        })
                    .Take(3)
                    .ToList();

            // =====================================================
            // سه ردیف نیرو
            // =====================================================

            for (int i = 0;
                 i < 3;
                 i++)
            {
                GuardScheduleItem item =
                    null;

                if (i < items.Count)
                {
                    item =
                        items[i];
                }

                PdfPCell passCell =
                    new PdfPCell();

                passCell.BackgroundColor =
                    BaseColor.WHITE;

                passCell.Border =
                    iTextSharp.text.Rectangle.LEFT_BORDER |
                    iTextSharp.text.Rectangle.RIGHT_BORDER |
                    iTextSharp.text.Rectangle.BOTTOM_BORDER;

                passCell.BorderColor =
                    new BaseColor(
                        225,
                        225,
                        225);

                passCell.BorderWidth =
                    1;

                passCell.Padding =
                    7;

                passCell.PaddingTop =
                    6;

                passCell.PaddingBottom =
                    6;

                passCell.RunDirection =
                    PdfWriter.RUN_DIRECTION_RTL;

                Paragraph passParagraph =
                    new Paragraph();

                passParagraph.Alignment =
                    Element.ALIGN_LEFT;
                // -----------------------------
                // اگر پاس وجود دارد
                // -----------------------------

                if (item != null)
                {
                    string roleText;

                    if (item.Pass == 1)
                    {
                        roleText =
                            "افسر جانشین";
                    }
                    else if (item.Pass == 2)
                    {
                        roleText =
                            "افسر سر";
                    }
                    else
                    {
                        roleText =
                            "افسر پاسدار";
                    }

                    Officer officer =
                        GetScheduleOfficer(
                            item);

                    string officerName =
                        officer != null
                            ? officer.Name
                            : "نیروی مناسب پیدا نشد";

                    // ---------------------------------
                    // متن دقیق:
                    // افسر جانشین: داریوش رحیمی
                    // ---------------------------------

                    Chunk roleChunk =
                        new Chunk(
                            roleText +
                            ": ",
                            roleFont);

                    Chunk nameChunk =
                        new Chunk(
                            officerName,
                            officerFont);

                    passParagraph.Add(
                        roleChunk);

                    passParagraph.Add(
                        nameChunk);
                }
                else
                {
                    passParagraph.Add(
                        new Chunk(
                            "",
                            officerFont));
                }

                passCell.AddElement(
                    passParagraph);

                card.AddCell(
                    passCell);
            }

            return card;
        }
        private BaseFont GetPdfBaseFont()
        {
            string fontsFolder =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Fonts);

            string tahomaPath =
                Path.Combine(
                    fontsFolder,
                    "tahoma.ttf");

            if (File.Exists(tahomaPath))
            {
                return BaseFont.CreateFont(
                    tahomaPath,
                    BaseFont.IDENTITY_H,
                    BaseFont.EMBEDDED);
            }

            string arialPath =
                Path.Combine(
                    fontsFolder,
                    "arial.ttf");

            if (File.Exists(arialPath))
            {
                return BaseFont.CreateFont(
                    arialPath,
                    BaseFont.IDENTITY_H,
                    BaseFont.EMBEDDED);
            }

            throw new FileNotFoundException(
                "فونت مناسب فارسی برای تولید PDF پیدا نشد.");
        }

        // =====================================================
        // خروجی PDF گزارشات
        // =====================================================

        private void ReportsPdfButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (schedule == null ||
                    schedule.Count == 0)
                {
                    MessageBox.Show(
                        "ابتدا برنامه نگهبانی را ایجاد کنید.",
                        "خروجی گزارشات",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                SaveFileDialog dialog =
                    new SaveFileDialog();

                dialog.Title =
                    "ذخیره PDF گزارشات";

                dialog.Filter =
                    "PDF Files (*.pdf)|*.pdf";

                dialog.FileName =
                    "گزارشات نگهبانی_" +
                    GetPersianDate(DateTime.Now)
                        .Replace("/", "-") +
                    ".pdf";

                bool? result =
                    dialog.ShowDialog();

                if (result != true)
                    return;

                CreateReportsPdf(
                    dialog.FileName);

                MessageBox.Show(
                    "PDF گزارشات با موفقیت ایجاد شد.\n\n" +
                    dialog.FileName,
                    "خروجی گزارشات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ایجاد PDF گزارشات:\n\n" +
                    ex.ToString(),
                    "خطای PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        // =====================================================
        // ساخت PDF گزارشات
        // سه صفحه:
        // صفحه 1 = پاسدار
        // صفحه 2 = جانشین
        // صفحه 3 = سر
        // =====================================================

        // =====================================================
        // PDF گزارشات فعلی برنامه
        // این Wrapper برای حفظ عملکرد قبلی برنامه است.
        // =====================================================
        private void CreateReportsPdf(
            string fileName)
        {
            CreateReportsPdf(
                fileName,
                schedule,
                currentProgramStartDate,
                guardOfficers,
                reserveOfficers,
                chiefOfficers,
                true);
        }


        // =====================================================
        // PDF گزارشات یک برنامه مشخص
        // این نسخه برای تاریخچه استفاده می‌شود.
        // =====================================================
        private void CreateReportsPdf(
            string fileName,
            List<GuardScheduleItem> sourceSchedule,
            DateTime startDate,
            List<Officer> reportGuardOfficers,
            List<Officer> reportReserveOfficers,
            List<Officer> reportChiefOfficers,
            bool includeReplacementReport)
        {
            Document document =
                new Document(
                    PageSize.A4,
                    25,
                    25,
                    25,
                    25);

            PdfWriter writer =
                PdfWriter.GetInstance(
                    document,
                    new FileStream(
                        fileName,
                        FileMode.Create,
                        FileAccess.Write));

            writer.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            document.Open();

            BaseFont baseFont =
                GetPdfBaseFont();

            if (sourceSchedule == null)
            {
                sourceSchedule =
                    new List<GuardScheduleItem>();
            }

            // =====================================================
            // افسران همان Snapshot
            // =====================================================

            List<Officer> historyGuardOfficers =
                reportGuardOfficers;

            List<Officer> historyReserveOfficers =
                reportReserveOfficers;

            List<Officer> historyChiefOfficers =
                reportChiefOfficers;

            // =====================================================
            // صفحه اول = پاسدار
            // =====================================================

            List<OfficerReportRow> guardReport =
                CreateOfficerReport(
                    historyGuardOfficers,
                    delegate (GuardScheduleItem item)
                    {
                        return item.Guard;
                    },
                    sourceSchedule,
                    startDate);

            CreateSingleOfficerReportPage(
                document,
                baseFont,
                "گزارش افسر پاسدار",
                guardReport);

            // =====================================================
            // صفحه دوم = جانشین
            // =====================================================

            document.NewPage();

            List<OfficerReportRow> reserveReport =
                CreateOfficerReport(
                    historyReserveOfficers,
                    delegate (GuardScheduleItem item)
                    {
                        return item.Reserve;
                    },
                    sourceSchedule,
                    startDate);

            CreateSingleOfficerReportPage(
                document,
                baseFont,
                "گزارش افسر جانشین",
                reserveReport);

            // =====================================================
            // صفحه سوم = افسر سر
            // =====================================================

            document.NewPage();

            List<OfficerReportRow> chiefReport =
                CreateOfficerReport(
                    historyChiefOfficers,
                    delegate (GuardScheduleItem item)
                    {
                        return item.Chief;
                    },
                    sourceSchedule,
                    startDate);

            CreateSingleOfficerReportPage(
                document,
                baseFont,
                "گزارش افسر سر",
                chiefReport);
            if (includeReplacementReport &&
                useReplacementOfficers)
            {
                document.NewPage();

                CreateReplacementReportPage(
                    document,
                    baseFont,
                    reportGuardOfficers,
                    reportReserveOfficers,
                    reportChiefOfficers);
            }
            document.Close();
            writer.Close();
        }

        // =====================================================
        // ساخت یک صفحه گزارش افسر
        // =====================================================
        private List<Officer> GetHistoryOfficers(
            List<GuardScheduleItem> sourceSchedule,
            Func<GuardScheduleItem, Officer> selector)
        {
            List<Officer> result =
                new List<Officer>();

            if (sourceSchedule == null)
                return result;

            foreach (GuardScheduleItem item in sourceSchedule)
            {
                Officer officer =
                    selector(item);

                if (officer == null)
                    continue;

                bool exists =
                    result.Any(
                        x =>
                            string.Equals(
                                x.Name,
                                officer.Name,
                                StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    result.Add(officer);
                }
            }

            return result;
        }
        private void CreateSingleOfficerReportPage(
            Document document,
            BaseFont baseFont,
            string title,
            List<OfficerReportRow> reportRows)
        {
            iTextSharp.text.Font titleFont =
                new iTextSharp.text.Font(
                    baseFont,
                    17,
                    iTextSharp.text.Font.BOLD,
                    BaseColor.BLACK);

            iTextSharp.text.Font normalFont =
                new iTextSharp.text.Font(
                    baseFont,
                    8,
                    iTextSharp.text.Font.NORMAL,
                    BaseColor.BLACK);

            iTextSharp.text.Font headerFont =
                new iTextSharp.text.Font(
                    baseFont,
                    8,
                    iTextSharp.text.Font.BOLD,
                    BaseColor.WHITE);

            iTextSharp.text.Font smallFont =
                new iTextSharp.text.Font(
                    baseFont,
                    7,
                    iTextSharp.text.Font.NORMAL,
                    new BaseColor(50, 50, 50));

            // =====================================================
            // سربرگ
            // =====================================================

            PdfPTable topTable =
                new PdfPTable(1);

            topTable.WidthPercentage =
                100;

            topTable.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            PdfPCell titleCell =
                new PdfPCell();

            titleCell.BackgroundColor =
                new BaseColor(238, 238, 238);

            titleCell.Border =
                Rectangle.BOX;

            titleCell.BorderColor =
                new BaseColor(180, 180, 180);

            titleCell.Padding =
                8;

            titleCell.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            Paragraph titleParagraph =
                new Paragraph(
                    title,
                    titleFont);

            titleParagraph.Alignment =
                Element.ALIGN_CENTER;

            titleCell.AddElement(
                titleParagraph);

            topTable.AddCell(
                titleCell);

            document.Add(
                topTable);

            document.Add(
                new Paragraph(" "));

            // =====================================================
            // تاریخ‌ها
            // =====================================================

            PdfPTable datesTable =
                new PdfPTable(2);

            datesTable.WidthPercentage =
                100;

            datesTable.SetWidths(
                new float[]
                {
            50,
            50
                });

            datesTable.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            PdfPCell createDateCell =
                new PdfPCell(
                    new Phrase(
                        "تاریخ ایجاد برنامه: " +
                        GetPersianDateForPdf(
                            currentProgramStartDate),
                        normalFont));

            createDateCell.Border =
                Rectangle.NO_BORDER;

            createDateCell.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            createDateCell.HorizontalAlignment =
                Element.ALIGN_RIGHT;

            PdfPCell printDateCell =
                new PdfPCell(
                    new Phrase(
                        "تاریخ چاپ: " +
                        GetPersianDateForPdf(
                            DateTime.Now),
                        normalFont));

            printDateCell.Border =
                Rectangle.NO_BORDER;

            printDateCell.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            printDateCell.HorizontalAlignment =
                Element.ALIGN_LEFT;

            datesTable.AddCell(
                createDateCell);

            datesTable.AddCell(
                printDateCell);

            document.Add(
                datesTable);

            document.Add(
                new Paragraph(" "));

            // =====================================================
            // جدول گزارش
            // دقیقاً مطابق ساختار عکس:
            //
            // ردیف | نام | امتیاز | تعداد پاس | روزهای پاس
            // =====================================================

            PdfPTable table =
                new PdfPTable(6);

            table.WidthPercentage =
                100;

            table.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            table.SetWidths(
                new float[]
                {
                    14,   // وضعیت
                    40,   // روزهای پاس
                    8,    // تعداد پاس
                    9,    // امتیاز
                    22,   // نام
                    7     // ردیف
                });

            table.SplitRows =
                true;

            table.SplitLate =
                false;

            AddReportHeaderCell(
                table,
                "ردیف",
                headerFont);

            AddReportHeaderCell(
                table,
                "نام",
                headerFont);

            AddReportHeaderCell(
                table,
                "امتیاز",
                headerFont);

            AddReportHeaderCell(
                table,
                "تعداد پاس",
                headerFont);

            AddReportHeaderCell(
                table,
                "روزهای پاس",
                headerFont);

            AddReportHeaderCell(
                table,
                "وضعیت",
                headerFont);

            if (reportRows != null)
            {
                foreach (OfficerReportRow row
                         in reportRows)
                {
                    AddReportBodyCell(
                        table,
                        row.RowNumber.ToString(),
                        normalFont);

                    AddReportBodyCell(
                        table,
                        row.Name,
                        normalFont);

                    AddReportBodyCell(
                        table,
                        row.Score,
                        normalFont);

                    AddReportBodyCell(
                        table,
                        row.PassCount.ToString(),
                        normalFont);

                    AddReportBodyCell(
                        table,
                        row.PassDays,
                        smallFont);
                    AddReportBodyCell(
                        table,
                        row.Status,
                        smallFont);
                }
            }

            document.Add(
                table);
        }
        // =====================================================
        // هدر جدول گزارش
        // =====================================================
        private void CreateReplacementReportPage(
    Document document,
    BaseFont baseFont,
    List<Officer> guardOfficers,
    List<Officer> reserveOfficers,
    List<Officer> chiefOfficers)
        {
            iTextSharp.text.Font titleFont =
                new iTextSharp.text.Font(
                    baseFont,
                    17,
                    iTextSharp.text.Font.BOLD,
                    BaseColor.BLACK);

            iTextSharp.text.Font normalFont =
                new iTextSharp.text.Font(
                    baseFont,
                    8,
                    iTextSharp.text.Font.NORMAL,
                    BaseColor.BLACK);

            iTextSharp.text.Font headerFont =
                new iTextSharp.text.Font(
                    baseFont,
                    8,
                    iTextSharp.text.Font.BOLD,
                    BaseColor.WHITE);

            // ==========================================
            // عنوان
            // ==========================================

            PdfPTable titleTable =
                new PdfPTable(1);

            titleTable.WidthPercentage =
                100;

            titleTable.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            PdfPCell titleCell =
                new PdfPCell();

            titleCell.Border =
                Rectangle.NO_BORDER;

            titleCell.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            titleCell.HorizontalAlignment =
                Element.ALIGN_CENTER;

            Paragraph titleParagraph =
                new Paragraph(
                    "گزارش نیروهای جایگزین",
                    titleFont);

            titleParagraph.Alignment =
                Element.ALIGN_CENTER;

            titleCell.AddElement(
                titleParagraph);

            titleTable.AddCell(
                titleCell);

            document.Add(
                titleTable);

            document.Add(
                new Paragraph(" "));

            // ==========================================
            // جدول
            // ==========================================

            PdfPTable table =
                new PdfPTable(5);

            table.WidthPercentage =
                100;

            table.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            table.SetWidths(
                new float[]
                {
            15,   // وضعیت
            18,   // سمت
            12,   // امتیاز
            48,   // نام
            7     // ردیف
                });

            table.SplitRows =
                true;

            table.SplitLate =
                false;

            AddReportHeaderCell(
                table,
                "ردیف",
                headerFont);

            AddReportHeaderCell(
                table,
                "نام",
                headerFont);

            AddReportHeaderCell(
                table,
                "امتیاز",
                headerFont);

            AddReportHeaderCell(
                table,
                "سمت",
                headerFont);

            AddReportHeaderCell(
                table,
                "وضعیت",
                headerFont);

            int rowNumber = 1;

            // ==========================================
            // تابع افزودن جایگزین
            // ==========================================

            Action<Officer, string> addReplacement =
                delegate (
                    Officer officer,
                    string role)
                {
                    if (officer == null)
                        return;

                    if (!officer.IsReplacement)
                        return;

                    AddReportBodyCell(
                        table,
                        rowNumber.ToString(),
                        normalFont);

                    AddReportBodyCell(
                        table,
                        officer.Name,
                        normalFont);

                    AddReportBodyCell(
                        table,
                        officer.Score.ToString(),
                        normalFont);

                    AddReportBodyCell(
                        table,
                        role,
                        normalFont);

                    AddReportBodyCell(
                        table,
                        "🔄 نیروی جایگزین",
                        normalFont);

                    rowNumber++;
                };

            // ==========================================
            // پاسدار
            // ==========================================

            if (guardOfficers != null)
            {
                foreach (Officer officer
                         in guardOfficers)
                {
                    addReplacement(
                        officer,
                        "افسر پاسدار");
                }
            }

            // ==========================================
            // جانشین
            // ==========================================

            if (reserveOfficers != null)
            {
                foreach (Officer officer
                         in reserveOfficers)
                {
                    addReplacement(
                        officer,
                        "افسر جانشین");
                }
            }

            // ==========================================
            // افسر سر
            // ==========================================

            if (chiefOfficers != null)
            {
                foreach (Officer officer
                         in chiefOfficers)
                {
                    addReplacement(
                        officer,
                        "افسر سر");
                }
            }

            // ==========================================
            // اگر جایگزینی وجود نداشت
            // ==========================================

            if (rowNumber == 1)
            {
                AddReportBodyCell(
                    table,
                    "-",
                    normalFont);

                AddReportBodyCell(
                    table,
                    "نیروی جایگزین وجود ندارد",
                    normalFont);

                AddReportBodyCell(
                    table,
                    "-",
                    normalFont);

                AddReportBodyCell(
                    table,
                    "-",
                    normalFont);

                AddReportBodyCell(
                    table,
                    "-",
                    normalFont);
            }

            document.Add(
                table);
        }
        private void AddReportHeaderCell(
            PdfPTable table,
            string text,
            iTextSharp.text.Font font)
        {
            PdfPCell cell =
                new PdfPCell(
                    new Phrase(
                        text,
                        font));

            cell.BackgroundColor =
                new BaseColor(110, 110, 110);

            cell.BorderColor =
                new BaseColor(85, 85, 85);

            cell.BorderWidth =
                1;

            cell.Padding =
                6;

            cell.HorizontalAlignment =
                Element.ALIGN_CENTER;

            cell.VerticalAlignment =
                Element.ALIGN_MIDDLE;

            cell.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            table.AddCell(
                cell);
        }


        // =====================================================
        // بدنه جدول گزارش
        // =====================================================

        private void AddReportBodyCell(
            PdfPTable table,
            string text,
            iTextSharp.text.Font font)
        {
            PdfPCell cell =
                new PdfPCell(
                    new Phrase(
                        text ?? "",
                        font));

            cell.BackgroundColor =
                BaseColor.WHITE;

            cell.BorderColor =
                new BaseColor(210, 210, 210);

            cell.BorderWidth =
                1;

            cell.Padding =
                7;

            cell.HorizontalAlignment =
                Element.ALIGN_CENTER;

            cell.VerticalAlignment =
                Element.ALIGN_MIDDLE;

            cell.RunDirection =
                PdfWriter.RUN_DIRECTION_RTL;

            cell.NoWrap =
                false;

            table.AddCell(
                cell);
        }
        private void PdfPlaceholder_Click()
        {
            MessageBox.Show(
                "بخش خروجی PDF هنوز در حال آماده‌سازی است.\n\n" +
                "این دکمه در مرحله بعد به سیستم تولید PDF متصل خواهد شد.",
                "خروجی PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        private void StartProgram_Click(
            object sender,
            RoutedEventArgs e)
        {
            StartupView.Visibility =
                Visibility.Collapsed;

            MainApplicationView.Visibility =
                Visibility.Visible;

            ShowDashboard();
        }

        // =====================================================
        // ورود به تنظیمات
        // =====================================================

        private void OpenSettings_Click(
            object sender,
            RoutedEventArgs e)
        {
            StartupView.Visibility =
                Visibility.Collapsed;

            MainApplicationView.Visibility =
                Visibility.Visible;

            ShowSettings();
        }

        // =====================================================
        // نمایش داشبورد
        // =====================================================

        private void ShowDashboard()
        {
            HideAllMainPages();

            DashboardView.Visibility =
                Visibility.Visible;
        }
        private void CreateProgramMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideAllMainPages();

            CreateProgramView.Visibility =
                Visibility.Visible;
        }
        // =====================================================
        // نمایش تنظیمات
        // =====================================================
        private void HideAllMainPages()
        {
            // =====================================================
            // داشبورد
            // =====================================================

            if (DashboardView != null)
            {
                DashboardView.Visibility =
                    Visibility.Collapsed;
            }


            // =====================================================
            // تنظیمات
            // =====================================================

            if (SettingsView != null)
            {
                SettingsView.Visibility =
                    Visibility.Collapsed;
            }


            // =====================================================
            // مدیریت افسرها
            // =====================================================

            if (OtherViews != null)
            {
                OtherViews.Visibility =
                    Visibility.Collapsed;
            }


            // =====================================================
            // ایجاد برنامه
            // =====================================================

            if (CreateProgramView != null)
            {
                CreateProgramView.Visibility =
                    Visibility.Collapsed;
            }


            // =====================================================
            // برنامه نگهبانی
            // =====================================================

            if (ScheduleView != null)
            {
                ScheduleView.Visibility =
                    Visibility.Collapsed;
            }


            // =====================================================
            // راهنما
            // =====================================================

            if (GuideView != null)
            {
                GuideView.Visibility =
                    Visibility.Collapsed;
            }


            // =====================================================
            // گزارشات
            // =====================================================

            if (ReportsView != null)
            {
                ReportsView.Visibility =
                    Visibility.Collapsed;
            }
            if (ReportsLoadingOverlay != null)
            {
                ReportsLoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
        private void ShowSettings()
        {
            HideAllMainPages();

            SettingsView.Visibility =
                Visibility.Visible;

            ShowSettingsSection("days");
        }
        // =====================================================
        // داشبورد
        // =====================================================

        private void DashboardMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowDashboard();
        }

        // =====================================================
        // تنظیمات
        // =====================================================

        private void SettingsMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettings();
        }

        // =====================================================
        // صفحات موقت
        // =====================================================
        private void OfficerManagementMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOtherPage(
                "👮",
                "مدیریت افسرها",
                "مدیریت افسران پاسدار، جانشین و سر");

            RefreshOfficerLists();
        }
        private void ShowOtherPage(
            string icon,
            string title,
            string description)
        {
            HideAllMainPages();

            OtherViews.Visibility =
                Visibility.Visible;

             //==========================================
             //نمایش تاریخچه
             //==========================================

            bool showHistory =
                title == "تاریخچه برنامه‌ها";

            if (HistoryView != null)
            {
                HistoryView.Visibility =
                    showHistory
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }

            if (OtherViewsContent != null)
            {
                OtherViewsContent.Visibility =
                    showHistory
                        ? Visibility.Collapsed
                        : Visibility.Visible;
            }

             //==========================================
             //حالت اولیه آکاردئون مدیریت نیروها
             //==========================================

            if (!showHistory)
            {
                if (GuardAccordionContent != null)
                {
                    GuardAccordionContent.Visibility =
                        Visibility.Visible;
                }

                if (ReserveAccordionContent != null)
                {
                    ReserveAccordionContent.Visibility =
                        Visibility.Collapsed;
                }

                if (ChiefAccordionContent != null)
                {
                    ChiefAccordionContent.Visibility =
                        Visibility.Collapsed;
                }

                if (GuardAccordionArrow != null)
                {
                    GuardAccordionArrow.Text =
                        "▼";
                }

                if (ReserveAccordionArrow != null)
                {
                    ReserveAccordionArrow.Text =
                        "▶";
                }

                if (ChiefAccordionArrow != null)
                {
                    ChiefAccordionArrow.Text =
                        "▶";
                }
            }

             //==========================================
             //بارگذاری تاریخچه
             //==========================================

            if (showHistory)
            {
                LoadHistoryCards();
            }
        }

        private void GuardOfficerMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOtherPage(
                "👮",
                "افسر پاسدار",
                "بخش مدیریت و ثبت افسران پاسدار");
        }

        private void ReserveOfficerMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOtherPage(
                "🔄",
                "افسر جانشین",
                "بخش مدیریت و ثبت افسران جانشین");
        }

        private void ChiefOfficerMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOtherPage(
                "⭐",
                "افسر سر",
                "بخش مدیریت و ثبت افسران سر");
        }

        private string GetPersianDate(DateTime date)
        {
            PersianCalendar pc =
                new PersianCalendar();

            return string.Format(
                "{0:0000}/{1:00}/{2:00}",
                pc.GetYear(date),
                pc.GetMonth(date),
                pc.GetDayOfMonth(date));
        }
        private string GetPersianDateForPdf(DateTime date)
        {
            PersianCalendar pc =
                new PersianCalendar();

            return string.Format(
                "{0:00}/{1:00}/{2:0000}",
                pc.GetDayOfMonth(date),
                pc.GetMonth(date),
                pc.GetYear(date));
        }
        private void ScheduleMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideAllMainPages();

            ScheduleView.Visibility =
                Visibility.Visible;


            if (ScheduleDaysInfoLabel != null)
            {
                ScheduleDaysInfoLabel.Text =
                    selectedProgramDays.ToString();
            }


            if (schedule != null &&
                schedule.Count > 0)
            {
                ShowSchedule();
            }
            else
            {
                ScheduleContainer.Children.Clear();

                if (ScheduleStatusLabel != null)
                {
                    ScheduleStatusLabel.Text =
                        "هنوز برنامه‌ای ساخته نشده است.";
                }
            }
        }

        private bool isReportsLoading = false;

        private async void ReportsMenu_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (isReportsLoading)
                return;

            isReportsLoading = true;

            try
            {
                if (MainContentScrollViewer != null)
                {
                    MainContentScrollViewer.ScrollToHome();
                }
                // =====================================================
                // مرحله 1 - نمایش صفحه گزارشات
                // =====================================================

                HideAllMainPages();

                ReportsView.Visibility =
                    Visibility.Visible;


                // =====================================================
                // مرحله 2 - نمایش محتوا در پشت Loader
                // =====================================================

                ReportsContent.Visibility =
                    Visibility.Visible;


                // =====================================================
                // مرحله 3 - نمایش Loader
                // =====================================================

                ReportsLoadingOverlay.Visibility =
                    Visibility.Visible;


                // =====================================================
                // مرحله 4 - Reset کامل Loader
                // =====================================================

                ReportsLoadingProgress.Value = 0;

                ReportsLoadingPercent.Text =
                    "0%";

                ReportsLoadingText.Text =
                    "در حال شروع...";

                ReportsLoadingStatus.Text =
                    "لطفاً صبر کنید...";


                // =====================================================
                // خیلی مهم:
                // اول خود صفحه گزارشات را Layout کن
                // =====================================================

                ReportsView.UpdateLayout();


                // =====================================================
                // اجازه Render شدن Loader
                // =====================================================

                await Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Render);


                await Task.Delay(60);


                // =====================================================
                // مرحله 1
                // =====================================================

                await UpdateReportProgress(
                    15,
                    "در حال بررسی اطلاعات نیروها...",
                    "مرحله 1 از 5");


                // =====================================================
                // مرحله 2
                // =====================================================

                await UpdateReportProgress(
                    30,
                    "در حال آماده‌سازی اطلاعات افسران...",
                    "مرحله 2 از 5");


                // =====================================================
                // ساخت گزارش‌ها در Background
                // =====================================================

                ReportDataResult reportData =
                    await Task.Run(
                        () =>
                        {
                            return new ReportDataResult
                            {
                                Reserve =
                                    CreateOfficerReport(
                                        reserveOfficers,
                                        delegate (GuardScheduleItem item)
                                        {
                                            return item.Reserve;
                                        }),

                                Chief =
                                    CreateOfficerReport(
                                        chiefOfficers,
                                        delegate (GuardScheduleItem item)
                                        {
                                            return item.Chief;
                                        }),

                                Guard =
                                    CreateOfficerReport(
                                        guardOfficers,
                                        delegate (GuardScheduleItem item)
                                        {
                                            return item.Guard;
                                        })
                            };
                        });


                // =====================================================
                // مرحله 3
                // =====================================================

                await UpdateReportProgress(
                    55,
                    "در حال آماده‌سازی جدول‌های گزارش...",
                    "مرحله 3 از 5");


                // =====================================================
                // اتصال اطلاعات به جدول‌ها
                // =====================================================

                ReserveReportGrid.ItemsSource =
                    reportData.Reserve;

                ChiefReportGrid.ItemsSource =
                    reportData.Chief;

                GuardReportGrid.ItemsSource =
                    reportData.Guard;


                // =====================================================
                // گزارش جایگزین‌ها
                // =====================================================

                UpdateReplacementReportIfNeeded();


                // =====================================================
                // مرحله 4
                // =====================================================

                await UpdateReportProgress(
                    75,
                    "در حال آماده‌سازی نهایی گزارش...",
                    "مرحله 4 از 5");


                // =====================================================
                // مرحله 5
                // =====================================================

                await UpdateReportProgress(
                    90,
                    "در حال آماده‌سازی جدول‌ها...",
                    "مرحله 5 از 5");


                // =====================================================
                // خیلی مهم:
                // Render واقعی تمام جدول‌ها
                // =====================================================

                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        ReserveReportGrid.UpdateLayout();
                        ChiefReportGrid.UpdateLayout();
                        GuardReportGrid.UpdateLayout();

                        if (ReplacementReportGrid != null &&
                            ReplacementReportSection != null &&
                            ReplacementReportSection.Visibility ==
                            Visibility.Visible)
                        {
                            ReplacementReportGrid.UpdateLayout();
                        }

                        ReportsContent.UpdateLayout();
                        ReportsView.UpdateLayout();
                    },
                    System.Windows.Threading.DispatcherPriority.Render);


                // =====================================================
                // یک Render اضافی
                // برای اینکه DataGrid کاملاً رسم شود
                // =====================================================

                await Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Render);


                await Task.Delay(50);


                // =====================================================
                // حالا واقعاً گزارش آماده است
                // =====================================================

                await UpdateReportProgress(
                    100,
                    "گزارش‌ها با موفقیت آماده شدند ✓",
                    "تکمیل شد");


                // =====================================================
                // Render نهایی
                // =====================================================

                await Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Render);


                // =====================================================
                // Loader را پنهان کن
                // فقط وقتی جدول‌ها آماده‌اند
                // =====================================================

                ReportsLoadingOverlay.Visibility =
                    Visibility.Collapsed;


                // =====================================================
                // یک Render نهایی بعد از حذف Loader
                // =====================================================

                await Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                ReportsLoadingOverlay.Visibility =
                    Visibility.Collapsed;

                ReportsContent.Visibility =
                    Visibility.Visible;

                MessageBox.Show(
                    "خطا در آماده‌سازی گزارشات:\n\n" +
                    ex.ToString(),
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                isReportsLoading = false;
            }
        }

        private void SavedPrograms_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowOtherPage(
                "🗂️",
                "تاریخچه برنامه‌ها",
                "مشاهده و مدیریت برنامه‌های ذخیره‌شده");
        }

        private void LoadHistoryCards()
        {
            if (HistoryProgramsContainer == null)
                return;

            HistoryProgramsContainer.Children.Clear();

            if (savedPrograms == null ||
                savedPrograms.Count == 0)
            {
                return;
            }

            foreach (GuardProgramHistory program
                     in savedPrograms)
            {
                if (program == null)
                    continue;

                Border card =
                    CreateHistoryProgramCard(program);

                HistoryProgramsContainer.Children.Add(
                    card);
            }
        }
        private Border CreateHistoryProgramCard(
    GuardProgramHistory program)
        {
            Border card =
                new Border
                {
                    Background =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                255, 255, 255)),

                    BorderBrush =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                226, 232, 240)),

                    BorderThickness =
                        new Thickness(1),

                    CornerRadius =
                        new CornerRadius(20),

                    Padding =
                        new Thickness(22),

                    Margin =
                        new Thickness(0, 0, 0, 16)
                };

            Grid grid =
                new Grid();

            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            // =====================================================
            // ردیف اول
            // =====================================================

            Grid headerGrid =
                new Grid();

            headerGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            headerGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                });

            StackPanel titlePanel =
                new StackPanel
                {
                    FlowDirection =
                        FlowDirection.RightToLeft
                };

            TextBlock title =
                new TextBlock
                {
                    Text = "برنامه نگهبانی",
                    FontSize = 19,
                    FontWeight = FontWeights.Bold,
                    Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                30, 41, 59))
                };

            TextBlock createdText =
                new TextBlock
                {
                    Text =
                        "ایجاد شده در: " +
                        GetPersianDate(program.CreatedAt),

                    FontSize = 12,

                    Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                100, 116, 139)),

                    Margin =
                        new Thickness(0, 6, 0, 0)
                };

            titlePanel.Children.Add(title);
            titlePanel.Children.Add(createdText);

            Grid.SetColumn(
                titlePanel,
                0);

            headerGrid.Children.Add(
                titlePanel);

            Border dayBadge =
                new Border
                {
                    Background =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                245, 243, 255)),

                    BorderBrush =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                221, 214, 254)),

                    BorderThickness =
                        new Thickness(1),

                    CornerRadius =
                        new CornerRadius(12),

                    Padding =
                        new Thickness(16, 9, 16, 9),

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            TextBlock dayBadgeText =
                new TextBlock
                {
                    Text =
                        program.ProgramDays +
                        " روز",

                    FontSize = 14,

                    FontWeight =
                        FontWeights.Bold,

                    Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                109, 40, 217))
                };

            dayBadge.Child =
                dayBadgeText;

            Grid.SetColumn(
                dayBadge,
                1);

            headerGrid.Children.Add(
                dayBadge);

            Grid.SetRow(
                headerGrid,
                0);

            grid.Children.Add(
                headerGrid);

            // =====================================================
            // اطلاعات برنامه
            // =====================================================

            Grid infoGrid =
                new Grid
                {
                    Margin =
                        new Thickness(0, 18, 0, 18)
                };

            infoGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            infoGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            infoGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            infoGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            infoGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            AddHistoryInfoBox(
                infoGrid,
                0,
                "📅",
                "تاریخ شروع",
                GetPersianDate(program.StartDate));

            AddHistoryInfoBox(
                infoGrid,
                1,
                "👮",
                "تعداد پاس",
                program.PassCount.ToString());

            AddHistoryInfoBox(
                infoGrid,
                2,
                "📆",
                "تعطیلات رسمی",
                program.HolidayCount.ToString());

            AddHistoryInfoBox(
                infoGrid,
                3,
                "🔄",
                "جایگزین",
                program.ReplacementEnabled
                    ? program.ReplacementCount.ToString()
                    : "استفاده نشده");

            AddHistoryInfoBox(
                infoGrid,
                4,
                "🗓️",
                "تعداد روز",
                program.ProgramDays.ToString());

            Grid.SetRow(
                infoGrid,
                1);

            grid.Children.Add(
                infoGrid);

            // =====================================================
            // سه دکمه عملیات
            // =====================================================

            Grid buttonsGrid =
                new Grid();

            buttonsGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            buttonsGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            buttonsGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            Button scheduleButton =
                CreateHistoryPdfButton(
                    "📄  PDF برنامه نگهبانی",
                    "#FFF6F6",
                    "#FECACA",
                    "#DC2626");

            scheduleButton.Tag =
                program;

            scheduleButton.Click +=
                HistorySchedulePdf_Click;

            Grid.SetColumn(
                scheduleButton,
                0);

            buttonsGrid.Children.Add(
                scheduleButton);
            Button deleteButton =
                CreateHistoryPdfButton(
                    "🗑️  حذف برنامه",
                    "#FFF1F2",
                    "#FECDD3",
                    "#E11D48");

            deleteButton.Tag =
                program;

            deleteButton.Click +=
                DeleteHistoryProgram_Click;

            Grid.SetColumn(
                deleteButton,
                2);

            buttonsGrid.Children.Add(
                deleteButton);
            Button reportsButton =
                CreateHistoryPdfButton(
                    "📊  PDF گزارشات",
                    "#F2FBF7",
                    "#A7F3D0",
                    "#059669");

            reportsButton.Tag =
                program;

            reportsButton.Click +=
                HistoryReportsPdf_Click;

            Grid.SetColumn(
                reportsButton,
                1);

            buttonsGrid.Children.Add(
                reportsButton);

            Grid.SetRow(
                buttonsGrid,
                2);

            grid.Children.Add(
                buttonsGrid);

            card.Child =
                grid;

            return card;
        }
        private void AddHistoryInfoBox(
    Grid grid,
    int column,
    string icon,
    string title,
    string value)
        {
            Border box =
                new Border
                {
                    Background =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                248, 250, 252)),

                    BorderBrush =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                226, 232, 240)),

                    BorderThickness =
                        new Thickness(1),

                    CornerRadius =
                        new CornerRadius(12),

                    Padding =
                        new Thickness(12),

                    Margin =
                        new Thickness(5, 0, 5, 0)
                };

            StackPanel panel =
                new StackPanel
                {
                    FlowDirection =
                        FlowDirection.RightToLeft
                };

            TextBlock iconText =
                new TextBlock
                {
                    Text = icon,
                    FontSize = 18
                };

            TextBlock titleText =
                new TextBlock
                {
                    Text = title,
                    FontSize = 11,
                    Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                100, 116, 139)),
                    Margin =
                        new Thickness(0, 5, 0, 0)
                };

            TextBlock valueText =
                new TextBlock
                {
                    Text = value,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                30, 41, 59)),
                    Margin =
                        new Thickness(0, 3, 0, 0)
                };

            panel.Children.Add(iconText);
            panel.Children.Add(titleText);
            panel.Children.Add(valueText);

            box.Child =
                panel;

            Grid.SetColumn(
                box,
                column);

            grid.Children.Add(
                box);
        }
        private Button CreateHistoryPdfButton(
            string text,
            string background,
            string border,
            string foreground)
        {
            Button button =
                new Button
                {
                    Content = text,

                    Height = 52,

                    Margin =
                        new Thickness(5, 0, 5, 0),

                    Background =
                        new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media.ColorConverter.ConvertFromString(
                                background)),

                    Foreground =
                        new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media.ColorConverter.ConvertFromString(
                                foreground)),

                    BorderBrush =
                        new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media.ColorConverter.ConvertFromString(
                                border)),

                    BorderThickness =
                        new Thickness(1),

                    FontSize = 13,

                    FontWeight =
                        FontWeights.Bold,

                    Cursor =
                        System.Windows.Input.Cursors.Hand
                };

            button.Template =
                CreateHistoryButtonTemplate();

            return button;
        }
        private ControlTemplate CreateHistoryButtonTemplate()
        {
            ControlTemplate template =
                new ControlTemplate(
                    typeof(Button));

            FrameworkElementFactory border =
                new FrameworkElementFactory(
                    typeof(Border));

            border.SetValue(
                Border.CornerRadiusProperty,
                new CornerRadius(12));

            border.SetValue(
                Border.BackgroundProperty,
                new TemplateBindingExtension(
                    Button.BackgroundProperty));

            border.SetValue(
                Border.BorderBrushProperty,
                new TemplateBindingExtension(
                    Button.BorderBrushProperty));

            border.SetValue(
                Border.BorderThicknessProperty,
                new TemplateBindingExtension(
                    Button.BorderThicknessProperty));

            FrameworkElementFactory content =
                new FrameworkElementFactory(
                    typeof(ContentPresenter));

            content.SetValue(
                ContentPresenter.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);

            content.SetValue(
                ContentPresenter.VerticalAlignmentProperty,
                VerticalAlignment.Center);

            border.AppendChild(
                content);

            template.VisualTree =
                border;

            return template;
        }
        private void DeleteHistoryProgram_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                Button button =
                    sender as Button;

                GuardProgramHistory program =
                    button?.Tag as GuardProgramHistory;

                if (program == null)
                    return;

                string programDate =
                    GetPersianDate(program.StartDate);

                MessageBoxResult result =
                    MessageBox.Show(
                        "آیا از حذف این برنامه از تاریخچه اطمینان دارید؟\n\n" +
                        "تاریخ شروع: " +
                        programDate +
                        "\n" +
                        "تعداد روز: " +
                        program.ProgramDays +
                        " روز\n\n" +
                        "این عملیات قابل بازگشت نیست.",
                        "حذف برنامه از تاریخچه",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                GuardProgramHistory itemToRemove =
                    savedPrograms.FirstOrDefault(
                        x => x.Id == program.Id);

                if (itemToRemove == null)
                    return;

                savedPrograms.Remove(
                    itemToRemove);

                SaveProgramHistory();

                //LoadHistoryCards();

                UpdateDashboardStatistics();

                MessageBox.Show(
                    "برنامه با موفقیت از تاریخچه حذف شد.",
                    "حذف برنامه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در حذف برنامه از تاریخچه:\n\n" +
                    ex.ToString(),
                    "خطای حذف تاریخچه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void HistorySchedulePdf_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                Button button =
                    sender as Button;

                GuardProgramHistory program =
                    button?.Tag as GuardProgramHistory;

                if (program == null)
                    return;

                if (program.Schedule == null ||
                    program.Schedule.Count == 0)
                {
                    MessageBox.Show(
                        "اطلاعات برنامه این سابقه کامل نیست.",
                        "خطای تاریخچه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                SaveFileDialog dialog =
                    new SaveFileDialog();

                dialog.Title =
                    "ذخیره PDF برنامه نگهبانی";

                dialog.Filter =
                    "PDF Files (*.pdf)|*.pdf";

                dialog.FileName =
                    "برنامه نگهبانی_" +
                    GetPersianDate(
                            program.StartDate)
                        .Replace("/", "-") +
                    "_" +
                    program.ProgramDays +
                    "روزه.pdf";

                bool? result =
                    dialog.ShowDialog();

                if (result != true)
                    return;

                CreateSchedulePdf(
                    dialog.FileName,
                    program.Schedule,
                    program.StartDate,
                    program.ProgramDays);

                MessageBox.Show(
                    "PDF برنامه نگهبانی این سابقه با موفقیت ایجاد شد.\n\n" +
                    dialog.FileName,
                    "خروجی PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ایجاد PDF برنامه نگهبانی:\n\n" +
                    ex.ToString(),
                    "خطای PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void HistoryReportsPdf_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                Button button =
                    sender as Button;

                GuardProgramHistory program =
                    button?.Tag as GuardProgramHistory;

                if (program == null)
                    return;

                if (program.Schedule == null ||
                    program.Schedule.Count == 0)
                {
                    MessageBox.Show(
                        "اطلاعات برنامه این سابقه کامل نیست.",
                        "خطای تاریخچه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                SaveFileDialog dialog =
                    new SaveFileDialog();

                dialog.Title =
                    "ذخیره PDF گزارشات";

                dialog.Filter =
                    "PDF Files (*.pdf)|*.pdf";

                dialog.FileName =
                    "گزارشات نگهبانی_" +
                    GetPersianDate(
                            program.StartDate)
                        .Replace("/", "-") +
                    "_" +
                    program.ProgramDays +
                    "روزه.pdf";

                bool? result =
                    dialog.ShowDialog();

                if (result != true)
                    return;

                CreateReportsPdf(
                    dialog.FileName,
                    program.Schedule,
                    program.StartDate,
                    guardOfficers,
                    reserveOfficers,
                    chiefOfficers,
                    true);

                MessageBox.Show(
                    "PDF گزارشات این سابقه با موفقیت ایجاد شد.\n\n" +
                    dialog.FileName,
                    "خروجی گزارشات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ایجاد PDF گزارشات:\n\n" +
                    ex.ToString(),
                    "خطای PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private string GetPersianWeekDay(
            DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Saturday:
                    return "شنبه";

                case DayOfWeek.Sunday:
                    return "یکشنبه";

                case DayOfWeek.Monday:
                    return "دوشنبه";

                case DayOfWeek.Tuesday:
                    return "سه شنبه";

                case DayOfWeek.Wednesday:
                    return "چهارشنبه";

                case DayOfWeek.Thursday:
                    return "پنجشنبه";

                case DayOfWeek.Friday:
                    return "جمعه";

                default:
                    return "";
            }
        }
        // =====================================================
        // بروزرسانی کامل گزارشات
        // =====================================================
        // =====================================================
        // حذف هاور پیش فرض دکمه‌ها
        // =====================================================

        private void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            RemoveButtonHoverEffects(this);
        }

        private void RemoveButtonHoverEffects(
            DependencyObject parent)
        {
            if (parent == null)
                return;

            int childCount =
                System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child =
                    System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                Button button =
                    child as Button;

                if (button != null)
                {
                    button.Template =
                        CreateNoHoverButtonTemplate();
                }

                RemoveButtonHoverEffects(child);
            }
        }


        // =====================================================
        // قالب دکمه بدون هاور
        // =====================================================

        private ControlTemplate CreateNoHoverButtonTemplate()
        {
            ControlTemplate template =
                new ControlTemplate(typeof(Button));

            FrameworkElementFactory border =
                new FrameworkElementFactory(typeof(Border));

            border.SetValue(
                Border.CornerRadiusProperty,
                new CornerRadius(10));

            border.SetBinding(
                Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                {
                    RelativeSource =
                        new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });

            border.SetBinding(
                Border.BorderBrushProperty,
                new System.Windows.Data.Binding("BorderBrush")
                {
                    RelativeSource =
                        new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });

            border.SetBinding(
                Border.BorderThicknessProperty,
                new System.Windows.Data.Binding("BorderThickness")
                {
                    RelativeSource =
                        new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });

            border.SetBinding(
                Border.PaddingProperty,
                new System.Windows.Data.Binding("Padding")
                {
                    RelativeSource =
                        new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });

            FrameworkElementFactory content =
                new FrameworkElementFactory(typeof(ContentPresenter));

            content.SetBinding(
                ContentPresenter.ContentProperty,
                new System.Windows.Data.Binding("Content")
                {
                    RelativeSource =
                        new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });

            content.SetBinding(
                ContentPresenter.ContentTemplateProperty,
                new System.Windows.Data.Binding("ContentTemplate")
                {
                    RelativeSource =
                        new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });

            content.SetBinding(
                ContentPresenter.HorizontalAlignmentProperty,
                new System.Windows.Data.Binding("HorizontalContentAlignment")
                {
                    RelativeSource =
                        new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });

            content.SetBinding(
                ContentPresenter.VerticalAlignmentProperty,
                new System.Windows.Data.Binding("VerticalContentAlignment")
                {
                    RelativeSource =
                        new System.Windows.Data.RelativeSource(
                            System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });

            border.AppendChild(content);

            template.VisualTree =
                border;

            return template;
        }
        // =====================================================
        // بروزرسانی گزارش نیروهای جایگزین
        // =====================================================
        private void UpdateReplacementReportIfNeeded()
        {
            try
            {
                // قطع اتصال قبلی
                if (ReplacementReportGrid != null)
                {
                    ReplacementReportGrid.ItemsSource = null;
                }

                // ساخت مجدد لیست جایگزین‌ها
                UpdateReplacementOfficersList();

                // اتصال لیست جدید
                if (ReplacementReportGrid != null)
                {
                    ReplacementReportGrid.ItemsSource =
                        replacementOfficers;
                }

                // نمایش یا مخفی کردن بخش گزارش جایگزین
                if (ReplacementReportSection != null)
                {
                    ReplacementReportSection.Visibility =
                        (useReplacementOfficers &&
                         replacementOfficers != null &&
                         replacementOfficers.Count > 0)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در بروزرسانی گزارش نیروهای جایگزین:\n\n" +
                    ex.Message,
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void UpdateReports()
        {
            try
            {
                ReserveReportGrid.ItemsSource =
                    CreateOfficerReport(
                        reserveOfficers,
                        delegate (GuardScheduleItem item)
                        {
                            return item.Reserve;
                        });

                ChiefReportGrid.ItemsSource =
                    CreateOfficerReport(
                        chiefOfficers,
                        delegate (GuardScheduleItem item)
                        {
                            return item.Chief;
                        });

                GuardReportGrid.ItemsSource =
                    CreateOfficerReport(
                        guardOfficers,
                        delegate (GuardScheduleItem item)
                        {
                            return item.Guard;
                        });

                // ==========================================
                // گزارش مستقل نیروهای جایگزین
                // ==========================================

                // ==========================================
                // گزارش نیروهای جایگزین
                // ==========================================

                // ابتدا اتصال قبلی را قطع می‌کنیم
                ReplacementReportGrid.ItemsSource = null;

                // ساخت مجدد لیست از روی IsReplacement
                UpdateReplacementOfficersList();

                // اتصال لیست جدید به جدول
                ReplacementReportGrid.ItemsSource =
                    replacementOfficers;

                // نمایش / مخفی کردن جدول
                if (ReplacementReportSection != null)
                {
                    ReplacementReportSection.Visibility =
                        (useReplacementOfficers &&
                         replacementOfficers.Count > 0)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "خطا در ساخت گزارشات:\n\n" +
                    ex.ToString(),
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =====================================================
        // ساخت گزارش یک لیست
        // =====================================================

        private List<OfficerReportRow> CreateOfficerReport(
            List<Officer> officers,
            Func<GuardScheduleItem, Officer> selector)
        {
            List<OfficerReportRow> result =
                new List<OfficerReportRow>();

            if (officers == null)
                return result;

            int rowNumber = 1;

            foreach (Officer officer in officers)
            {
                if (officer == null)
                    continue;

                List<GuardScheduleItem> assignments =
                    schedule
                        .Where(
                            delegate (GuardScheduleItem item)
                            {
                                Officer assigned =
                                    selector(item);

                                if (assigned == null)
                                    return false;

                                return string.Equals(
                                    assigned.Name,
                                    officer.Name,
                                    StringComparison.OrdinalIgnoreCase);
                            })
                        .OrderBy(
                            delegate (GuardScheduleItem item)
                            {
                                return item.Day;
                            })
                        .ToList();


                // ==========================================
                // روزهای پاس
                // ==========================================

                List<string> passDays =
                    new List<string>();

                foreach (GuardScheduleItem item
                         in assignments)
                {
                    DateTime date =
                        currentProgramStartDate.AddDays(
                            item.Day - 1);

                    string text =
                        GetPersianDate(date) +
                        " (" +
                        GetPersianWeekDay(
                            date.DayOfWeek) +
                        ")";

                    passDays.Add(text);
                }


                // ==========================================
                // تعطیلات رسمی
                // ==========================================

                List<string> holidayDays =
                    new List<string>();

                foreach (GuardScheduleItem item
                         in assignments)
                {
                    DateTime date =
                        currentProgramStartDate.AddDays(
                            item.Day - 1);

                    if (IsOfficialHoliday(date))
                    {
                        string holidayName =
                            GetOfficialHolidayName(date);

                        string text =
                            GetPersianDate(date);

                        if (!string.IsNullOrWhiteSpace(
                                holidayName))
                        {
                            text +=
                                " — " +
                                holidayName;
                        }

                        holidayDays.Add(text);
                    }
                }


                // ==========================================
                // وضعیت نیرو
                // ==========================================

                string status =
                    officer.IsReplacement
                        ? "🔄 نیروی جایگزین"
                        : "نیروی عادی";


                // ==========================================
                // ساخت ردیف
                // ==========================================

                OfficerReportRow row =
                    new OfficerReportRow();

                row.RowNumber =
                    rowNumber;

                row.Name =
                    officer.Name;

                row.Score =
                    officer.Score.ToString();

                row.PassCount =
                    assignments.Count;

                row.PassDays =
                    passDays.Count > 0
                        ? string.Join(
                            "، ",
                            passDays)
                        : "بدون پاس";

                row.Status =
                    status;

                row.Holidays =
                    holidayDays.Count > 0
                        ? string.Join(
                            "\n",
                            holidayDays)
                        : "ندارد";

                result.Add(row);

                rowNumber++;
            }

            return result;
        }
        private List<OfficerReportRow> CreateOfficerReport(
            List<Officer> officers,
            Func<GuardScheduleItem, Officer> selector,
            List<GuardScheduleItem> sourceSchedule,
            DateTime startDate)
        {
            List<OfficerReportRow> result =
                new List<OfficerReportRow>();

            if (officers == null)
                return result;

            if (sourceSchedule == null)
                sourceSchedule =
                    new List<GuardScheduleItem>();

            int rowNumber = 1;

            foreach (Officer officer in officers)
            {
                if (officer == null)
                    continue;

                List<GuardScheduleItem> assignments =
                    sourceSchedule
                        .Where(
                            delegate (GuardScheduleItem item)
                            {
                                Officer assigned =
                                    selector(item);

                                if (assigned == null)
                                    return false;

                                return string.Equals(
                                    assigned.Name,
                                    officer.Name,
                                    StringComparison.OrdinalIgnoreCase);
                            })
                        .OrderBy(
                            delegate (GuardScheduleItem item)
                            {
                                return item.Day;
                            })
                        .ThenBy(
                            delegate (GuardScheduleItem item)
                            {
                                return item.Pass;
                            })
                        .ToList();

                OfficerReportRow row =
                    new OfficerReportRow();

                row.RowNumber =
                    rowNumber++;

                row.Name =
                    officer.Name;

                row.Score =
                    officer.Score.ToString();

                row.PassCount =
                    assignments.Count;

                List<string> passDays =
                    new List<string>();

                foreach (GuardScheduleItem item
                         in assignments)
                {
                    DateTime date =
                        startDate.AddDays(
                            item.Day - 1);

                    string text =
                        GetPersianDate(date) +
                        " (" +
                        GetPersianWeekDay(
                            date.DayOfWeek) +
                        ")";

                    passDays.Add(text);
                }

                row.PassDays =
                    passDays.Count > 0
                        ? string.Join(
                            "، ",
                            passDays)
                        : "بدون پاس";

                string status =
                    officer.IsReplacement
                        ? "🔄 نیروی جایگزین"
                        : "نیروی عادی";

                row.Status =
                    status;

                List<string> holidays =
                    assignments
                        .Select(
                            delegate (GuardScheduleItem item)
                            {
                                DateTime assignmentDate =
                                    startDate.AddDays(
                                        item.Day - 1);

                                string holidayName =
                                    GetOfficialHolidayName(
                                        assignmentDate);

                                return holidayName;
                            })
                        .Where(
                            delegate (string value)
                            {
                                return !string.IsNullOrWhiteSpace(value);
                            })
                        .Distinct()
                        .ToList();

                row.Holidays =
                    string.Join(
                        "، ",
                        holidays.ToArray());

                result.Add(row);
            }

            return result;
        }

    }

}