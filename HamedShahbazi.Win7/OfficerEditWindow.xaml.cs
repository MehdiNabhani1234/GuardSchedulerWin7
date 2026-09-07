using System;
using System.Windows;
using System.Windows.Controls;

namespace HamedShahbazi.Win7
{
    public partial class OfficerEditWindow : Window
    {
        public string OfficerName { get; private set; }

        public double OfficerScore { get; private set; }


        // =====================================================
        // سازنده
        // =====================================================

        public OfficerEditWindow(
            string officerName,
            double officerScore)
        {
            InitializeComponent();


            OfficerName =
                officerName ?? "";


            OfficerScore =
                officerScore;


            // ==============================================
            // مقدار اولیه نام
            // ==============================================

            OfficerNameTextBox.Text =
                OfficerName;


            // ==============================================
            // مقدار اولیه امتیاز
            // ==============================================

            SetScoreSelection(
                OfficerScore);


            // ==============================================
            // فوکوس روی نام
            // ==============================================

            Loaded +=
                delegate
                {
                    OfficerNameTextBox.Focus();

                    OfficerNameTextBox.SelectAll();
                };
        }


        // =====================================================
        // انتخاب امتیاز
        // =====================================================

        private void SetScoreSelection(
            double score)
        {
            for (int i = 0;
                 i < OfficerScoreComboBox.Items.Count;
                 i++)
            {
                ComboBoxItem item =
                    OfficerScoreComboBox.Items[i]
                    as ComboBoxItem;


                if (item == null)
                    continue;


                double itemScore;


                if (!double.TryParse(
                        item.Content.ToString(),
                        out itemScore))
                {
                    continue;
                }


                if (Math.Abs(
                        itemScore - score) < 0.0001)
                {
                    OfficerScoreComboBox.SelectedIndex =
                        i;

                    return;
                }
            }


            // حالت پیش‌فرض

            OfficerScoreComboBox.SelectedIndex =
                0;
        }


        // =====================================================
        // ثبت تغییرات
        // =====================================================

        private void SaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string name =
                OfficerNameTextBox.Text.Trim();


            // ==============================================
            // بررسی نام
            // ==============================================

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "لطفاً نام و نام خانوادگی افسر را وارد کنید.",
                    "نام نامعتبر",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                OfficerNameTextBox.Focus();

                return;
            }


            // ==============================================
            // بررسی امتیاز
            // ==============================================

            if (OfficerScoreComboBox.SelectedItem == null)
            {
                MessageBox.Show(
                    "لطفاً امتیاز افسر را انتخاب کنید.",
                    "امتیاز نامعتبر",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                OfficerScoreComboBox.Focus();

                return;
            }


            ComboBoxItem selectedItem =
                OfficerScoreComboBox.SelectedItem
                as ComboBoxItem;


            if (selectedItem == null)
            {
                MessageBox.Show(
                    "امتیاز انتخاب‌شده معتبر نیست.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }


            double score;


            if (!double.TryParse(
                    selectedItem.Content.ToString(),
                    out score))
            {
                MessageBox.Show(
                    "امتیاز انتخاب‌شده معتبر نیست.",
                    "خطا",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }


            // ==============================================
            // ثبت نهایی
            // ==============================================

            OfficerName =
                name;


            OfficerScore =
                score;


            DialogResult =
                true;
        }


        // =====================================================
        // انصراف
        // =====================================================

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult =
                false;
        }
    }
}