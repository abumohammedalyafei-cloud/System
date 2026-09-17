using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DukkaniPOS.Models;
using DukkaniPOS.Services;

namespace DukkaniPOS
{
    public enum ToastType
    {
        Success,
        Warning,
        Error
    }

    public partial class MainWindow : Window
    {
        private readonly InventoryService _inventoryService;
        private readonly POSService _posService;
        private readonly DebtService _debtService;
        private readonly AnalyticsService _analyticsService;

        private readonly List<CartItem> _cart = new List<CartItem>();
        private Customer? _selectedCustomer;
        private DispatcherTimer? _toastTimer;

        public MainWindow()
        {
            InitializeComponent();

            _inventoryService = new InventoryService();
            _debtService = new DebtService();
            _posService = new POSService(_debtService);
            _analyticsService = new AnalyticsService(_inventoryService);

            DpAnalyticsDate.SelectedDate = DateTime.Today;
            RefreshAllData();

            RefocusBarcode();
        }

        #region Keyboard Shortcuts & Auto-Focus

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (ModalQuickSearch.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    CloseQuickSearch();
                    e.Handled = true;
                    return;
                }
            }

            if (ModalVoidSale.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    CloseVoidSaleModal();
                    e.Handled = true;
                    return;
                }
            }

            switch (e.Key)
            {
                case Key.F1:
                    NavPOS_Click(this, new RoutedEventArgs());
                    BtnPayCash_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F2:
                    NavPOS_Click(this, new RoutedEventArgs());
                    BtnPayCredit_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F3:
                    OpenQuickSearch();
                    e.Handled = true;
                    break;
                case Key.F5:
                    RefreshCurrentViewData();
                    ShowToast("تم تحديث كافة الجداول والبيانات بنجاح", ToastType.Success);
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (ViewPOS.Visibility == Visibility.Visible && DgCart.SelectedItem is CartItem selectedItem)
                    {
                        _cart.Remove(selectedItem);
                        UpdateCartUI();
                        ShowToast("تم حذف الصنف المحدد من السلة", ToastType.Warning);
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    if (ViewPOS.Visibility == Visibility.Visible && _cart.Count > 0)
                    {
                        _cart.Clear();
                        UpdateCartUI();
                        ShowToast("تم تفريغ السلة الحالية", ToastType.Warning);
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void RefocusBarcode()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (ViewPOS.Visibility == Visibility.Visible && 
                    ModalQuickSearch.Visibility != Visibility.Visible && 
                    ModalVoidSale.Visibility != Visibility.Visible)
                {
                    TxtBarcode.Focus();
                    TxtBarcode.SelectAll();
                }
            }));
        }

        #endregion

        #region Toast Notification System (Snackbar)

        public void ShowToast(string message, ToastType type)
        {
            _toastTimer?.Stop();

            TxtToastMessage.Text = message;

            switch (type)
            {
                case ToastType.Success:
                    ToastContainer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46"));
                    TxtToastIcon.Text = "🟢";
                    break;
                case ToastType.Warning:
                    ToastContainer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E"));
                    TxtToastIcon.Text = "🟡";
                    break;
                case ToastType.Error:
                    ToastContainer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                    TxtToastIcon.Text = "🔴";
                    break;
            }

            ToastContainer.Visibility = Visibility.Visible;

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _toastTimer.Tick += (s, args) =>
            {
                ToastContainer.Visibility = Visibility.Collapsed;
                _toastTimer.Stop();
            };
            _toastTimer.Start();
        }

        private void BtnDismissToast_Click(object sender, RoutedEventArgs e)
        {
            ToastContainer.Visibility = Visibility.Collapsed;
            _toastTimer?.Stop();
        }

        #endregion

        #region Navigation Handling

        private void NavPOS_Click(object sender, RoutedEventArgs e)
        {
            ShowView(ViewPOS);
            SetActiveButton(BtnNavPOS);
            RefocusBarcode();
        }

        private void NavInventory_Click(object sender, RoutedEventArgs e)
        {
            ShowView(ViewInventory);
            SetActiveButton(BtnNavInventory);
            RefreshInventoryGrid();
        }

        private void NavDebts_Click(object sender, RoutedEventArgs e)
        {
            ShowView(ViewDebts);
            SetActiveButton(BtnNavDebts);
            RefreshCustomersGrid();
        }

        private void NavAnalytics_Click(object sender, RoutedEventArgs e)
        {
            ShowView(ViewAnalytics);
            SetActiveButton(BtnNavAnalytics);
            LoadAnalyticsData();
        }

        private void ShowView(Grid viewToShow)
        {
            ViewPOS.Visibility = Visibility.Collapsed;
            ViewInventory.Visibility = Visibility.Collapsed;
            ViewDebts.Visibility = Visibility.Collapsed;
            ViewAnalytics.Visibility = Visibility.Collapsed;

            viewToShow.Visibility = Visibility.Visible;
        }

        private void SetActiveButton(Button activeBtn)
        {
            BtnNavPOS.Style = (Style)FindResource("SidebarBtn");
            BtnNavInventory.Style = (Style)FindResource("SidebarBtn");
            BtnNavDebts.Style = (Style)FindResource("SidebarBtn");
            BtnNavAnalytics.Style = (Style)FindResource("SidebarBtn");

            activeBtn.Style = (Style)FindResource("ActiveSidebarBtn");
        }

        private void RefreshCurrentViewData()
        {
            if (ViewPOS.Visibility == Visibility.Visible) RefreshCustomersCombo();
            else if (ViewInventory.Visibility == Visibility.Visible) RefreshInventoryGrid();
            else if (ViewDebts.Visibility == Visibility.Visible) RefreshCustomersGrid();
            else if (ViewAnalytics.Visibility == Visibility.Visible) LoadAnalyticsData();
        }

        #endregion

        #region Data Refresh Methods

        private void RefreshAllData()
        {
            RefreshCustomersCombo();
            RefreshInventoryGrid();
            RefreshCustomersGrid();
            LoadAnalyticsData();
        }

        private void RefreshCustomersCombo()
        {
            var customers = _debtService.GetAllCustomers();
            CboCustomers.ItemsSource = customers;
            if (customers.Count > 0 && CboCustomers.SelectedIndex == -1)
            {
                CboCustomers.SelectedIndex = 0;
            }
        }

        private void RefreshInventoryGrid()
        {
            DgProducts.ItemsSource = _inventoryService.GetAllProducts();
        }

        private void RefreshCustomersGrid()
        {
            var customers = _debtService.GetAllCustomers();
            DgCustomers.ItemsSource = customers;
            CboCustomers.ItemsSource = customers;
        }

        private void LoadAnalyticsData()
        {
            DateTime targetDate = DpAnalyticsDate.SelectedDate ?? DateTime.Today;
            var report = _analyticsService.GetDailyReport(targetDate);

            TxtStatRevenue.Text = $"{report.TotalRevenue:N0} YER";
            TxtStatCost.Text = $"{report.TotalCapitalCost:N0} YER";

            string sign = report.NetProfit >= 0 ? "+" : "";
            TxtStatProfit.Text = $"{sign}{report.NetProfit:N0} YER";

            DgLowStock.ItemsSource = report.LowStockProducts;
        }

        #endregion

        #region POS Cashier Logic & Void Sale Modal

        private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddItemToCartFromInput();
            }
        }

        private void BtnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            AddItemToCartFromInput();
        }

        private void AddItemToCartFromInput()
        {
            string input = TxtBarcode.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ShowToast("يرجى إدخال الباركود أو رمز المنتج", ToastType.Warning);
                return;
            }

            int qtyToAdd = 1;
            string code = input;

            if (input.Contains('*'))
            {
                var parts = input.Split('*');
                if (parts.Length == 2 && int.TryParse(parts[0], out int parsedQty) && parsedQty > 0)
                {
                    qtyToAdd = parsedQty;
                    code = parts[1].Trim();
                }
            }

            var product = _inventoryService.GetProductById(code);
            if (product == null)
            {
                ShowToast($"الصنف ذو الباركود '{code}' غير موجود بالمخزن!", ToastType.Error);
                TxtBarcode.SelectAll();
                return;
            }

            if (product.StockQuantity <= 0)
            {
                ShowToast($"المنتج '{product.ProductName}' نفدت كميته بالكامل بالمخزن", ToastType.Warning);
                return;
            }

            var existingItem = _cart.FirstOrDefault(c => c.Product.ProductID == product.ProductID);
            if (existingItem != null)
            {
                if (existingItem.Quantity + qtyToAdd > product.StockQuantity)
                {
                    ShowToast($"لا يمكن إضافة {qtyToAdd} قطعة! المتاح بالمخزن: {product.StockQuantity}", ToastType.Warning);
                    return;
                }
                existingItem.Quantity += qtyToAdd;
            }
            else
            {
                if (qtyToAdd > product.StockQuantity)
                {
                    ShowToast($"الكمية المطلوبة ({qtyToAdd}) تتجاوز المتاح بالمخزن ({product.StockQuantity})", ToastType.Warning);
                    return;
                }
                _cart.Add(new CartItem { Product = product, Quantity = qtyToAdd });
            }

            TxtBarcode.Clear();
            UpdateCartUI();
            ShowToast($"تمت إضافة ({qtyToAdd}) قطعة من '{product.ProductName}' للسلة", ToastType.Success);
            RefocusBarcode();
        }

        private void BtnRemoveCartItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CartItem item)
            {
                _cart.Remove(item);
                UpdateCartUI();
                ShowToast("تم حذف الصنف من السلة", ToastType.Warning);
                RefocusBarcode();
            }
        }

        private void BtnClearCart_Click(object sender, RoutedEventArgs e)
        {
            _cart.Clear();
            UpdateCartUI();
            ShowToast("تم تفريغ سلة المبيعات", ToastType.Warning);
            RefocusBarcode();
        }

        private void UpdateCartUI()
        {
            DgCart.ItemsSource = null;
            DgCart.ItemsSource = _cart;

            decimal total = _cart.Sum(item => item.TotalAmount);
            TxtGrandTotal.Text = $"{total:N0} YER";
        }

        private void BtnPayCash_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0)
            {
                ShowToast("سلة المبيعات فارغة! أدخل أصناف أولاً", ToastType.Warning);
                RefocusBarcode();
                return;
            }

            try
            {
                decimal total = _cart.Sum(i => i.TotalAmount);
                long saleId = _posService.ProcessSale(_cart, null);

                ShowToast($"تمت عملية البيع بنجاح! رقم الفاتورة: #{saleId} (الإجمالي: {total:N0} YER)", ToastType.Success);
                ShowReceiptDialog(saleId, "دفع نقدي كاش (CASH)");

                _cart.Clear();
                UpdateCartUI();
                RefreshAllData();
                RefocusBarcode();
            }
            catch (Exception ex)
            {
                ShowToast($"خطأ في عملية البيع: {ex.Message}", ToastType.Error);
            }
        }

        private void BtnPayCredit_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0)
            {
                ShowToast("سلة المبيعات فارغة! أدخل أصناف أولاً", ToastType.Warning);
                RefocusBarcode();
                return;
            }

            if (CboCustomers.SelectedValue == null)
            {
                ShowToast("يرجى اختيار العميل المسجل لتسجيل البيع الآجل", ToastType.Warning);
                return;
            }

            int customerId = Convert.ToInt32(CboCustomers.SelectedValue);
            var customer = _debtService.GetCustomerById(customerId);
            if (customer == null)
            {
                ShowToast("العميل المختار غير موجود بالسجلات", ToastType.Error);
                return;
            }

            try
            {
                decimal total = _cart.Sum(i => i.TotalAmount);
                long saleId = _posService.ProcessSale(_cart, customerId);

                ShowToast($"تم تسجيل البيع الآجل بنجاح! رقم الفاتورة: #{saleId} على حساب ({customer.CustomerName})", ToastType.Success);
                ShowReceiptDialog(saleId, $"بيع آجل - حساب ({customer.CustomerName})");

                _cart.Clear();
                UpdateCartUI();
                RefreshAllData();
                RefocusBarcode();
            }
            catch (Exception ex)
            {
                ShowToast($"فشل البيع الآجل: {ex.Message}", ToastType.Error);
            }
        }

        private void ShowReceiptDialog(long saleId, string payMethodStr)
        {
            decimal total = _cart.Sum(i => i.TotalAmount);
            string receiptText = $"=======================================\n" +
                                 $"           متاجر دكاني DUKKANI          \n" +
                                 $"         مدينة البيضاء - اليمن         \n" +
                                 $"رقم الفاتورة والمعاملة : #{saleId}\n" +
                                 $"التاريخ والوقت         : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                 $"---------------------------------------\n";

            foreach (var item in _cart)
            {
                receiptText += $"{item.Product.ProductName} x {item.Quantity} = {item.TotalAmount:N0} YER\n";
            }

            receiptText += $"---------------------------------------\n" +
                           $"المبلغ الإجمالي المطلـوب : {total:N0} YER\n" +
                           $"طريقة الدفــــــع       : {payMethodStr}\n" +
                           $"=======================================\n" +
                           $"   احتفظ برقم الفاتورة #{saleId} للاسترجاع   \n" +
                           $"       شكراً لتسوقكم معنا!             ";

            MessageBox.Show(receiptText, $"🧾 فاتورة رقم #{saleId} - دكاني POS", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Void Sale Modal Actions
        private void BtnOpenVoidSale_Click(object sender, RoutedEventArgs e)
        {
            ModalVoidSale.Visibility = Visibility.Visible;
            TxtVoidSaleId.Clear();
            TxtVoidSaleId.Focus();
        }

        private void BtnCloseVoidSale_Click(object sender, RoutedEventArgs e)
        {
            CloseVoidSaleModal();
        }

        private void CloseVoidSaleModal()
        {
            ModalVoidSale.Visibility = Visibility.Collapsed;
            RefocusBarcode();
        }

        private void BtnConfirmVoidSale_Click(object sender, RoutedEventArgs e)
        {
            if (!long.TryParse(TxtVoidSaleId.Text.Trim(), out long saleId) || saleId <= 0)
            {
                ShowToast("يرجى إدخال رقم فاتورة صحيح أكبر من الصفر", ToastType.Warning);
                return;
            }

            try
            {
                if (_posService.VoidSale(saleId))
                {
                    ShowToast($"تم إلغاء الفاتورة رقم #{saleId} وإرجاع الكميات للمخزن بنجاح!", ToastType.Success);
                    CloseVoidSaleModal();
                    RefreshAllData();
                }
            }
            catch (Exception ex)
            {
                ShowToast($"فشل إلغاء الفاتورة: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Quick Search Modal Dialog (F3)

        private void BtnOpenQuickSearch_Click(object sender, RoutedEventArgs e)
        {
            OpenQuickSearch();
        }

        private void OpenQuickSearch()
        {
            ModalQuickSearch.Visibility = Visibility.Visible;
            TxtSearchQuery.Clear();
            TxtSearchQuery.Focus();
            PerformQuickSearch("");
        }

        private void CloseQuickSearch()
        {
            ModalQuickSearch.Visibility = Visibility.Collapsed;
            RefocusBarcode();
        }

        private void BtnCloseQuickSearch_Click(object sender, RoutedEventArgs e)
        {
            CloseQuickSearch();
        }

        private void TxtSearchQuery_TextChanged(object sender, TextChangedEventArgs e)
        {
            PerformQuickSearch(TxtSearchQuery.Text.Trim());
        }

        private void PerformQuickSearch(string query)
        {
            var allProducts = _inventoryService.GetAllProducts();
            if (string.IsNullOrEmpty(query))
            {
                DgSearchResults.ItemsSource = allProducts;
            }
            else
            {
                DgSearchResults.ItemsSource = allProducts.Where(p =>
                    p.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.ProductID.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        private void BtnAddSearchResultToCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Product product)
            {
                TxtBarcode.Text = product.ProductID;
                AddItemToCartFromInput();
                ShowToast($"تمت إضافة '{product.ProductName}' للسلة", ToastType.Success);
            }
        }

        #endregion

        #region Inventory Logic & CSV Export

        private void BtnRefreshInventory_Click(object sender, RoutedEventArgs e)
        {
            RefreshInventoryGrid();
            ShowToast("تم تحديث جدول المخزون", ToastType.Success);
        }

        private void BtnExportInventoryCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var products = _inventoryService.GetAllProducts();
                string path = ExportService.ExportProductsToCsv(products);
                ShowToast($"تم تصدير المخزون كملف CSV إلى: {path}", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast($"فشل تصدير CSV: {ex.Message}", ToastType.Error);
            }
        }

        private void BtnSaveNewProduct_Click(object sender, RoutedEventArgs e)
        {
            string barcode = TxtNewBarcode.Text.Trim();
            string name = TxtNewProdName.Text.Trim();

            if (string.IsNullOrEmpty(barcode) || string.IsNullOrEmpty(name))
            {
                ShowToast("يرجى إدخال الباركود واسم المنتج", ToastType.Warning);
                return;
            }

            if (!decimal.TryParse(TxtNewCost.Text.Trim(), out decimal cost) || cost < 0 ||
                !decimal.TryParse(TxtNewSelling.Text.Trim(), out decimal selling) || selling < 0 ||
                !int.TryParse(TxtNewQty.Text.Trim(), out int qty) || qty < 0 ||
                !int.TryParse(TxtNewMinWarn.Text.Trim(), out int minWarn) || minWarn < 0)
            {
                ShowToast("يرجى إدخال قيم عددية صحيحة للأسعار والكميات", ToastType.Error);
                return;
            }

            var p = new Product
            {
                ProductID = barcode,
                ProductName = name,
                CostPrice = cost,
                SellingPrice = selling,
                StockQuantity = qty,
                MinStockWarning = minWarn
            };

            try
            {
                if (_inventoryService.AddProduct(p))
                {
                    ShowToast($"تم حفظ الصنف '{name}' بنجاح في المخزن!", ToastType.Success);
                    TxtNewBarcode.Clear();
                    TxtNewProdName.Clear();
                    TxtNewCost.Clear();
                    TxtNewSelling.Clear();
                    TxtNewQty.Clear();
                    RefreshInventoryGrid();
                }
            }
            catch (Exception ex)
            {
                ShowToast($"فشل حفظ الصنف: {ex.Message}", ToastType.Error);
            }
        }

        private void BtnConfirmIntake_Click(object sender, RoutedEventArgs e)
        {
            string barcode = TxtIntakeBarcode.Text.Trim();
            if (string.IsNullOrEmpty(barcode))
            {
                ShowToast("يرجى إدخال باركود الصنف المورد", ToastType.Warning);
                return;
            }

            if (!int.TryParse(TxtIntakeQty.Text.Trim(), out int qtyAdded) || qtyAdded <= 0)
            {
                ShowToast("يرجى إدخال كمية مضافة أكبر من الصفر", ToastType.Warning);
                return;
            }

            decimal.TryParse(TxtIntakeCost.Text.Trim(), out decimal unitCost);

            try
            {
                if (_inventoryService.StockIntake(barcode, qtyAdded, unitCost))
                {
                    ShowToast("تم توريد الكمية وتحديث المخزون بنجاح!", ToastType.Success);
                    TxtIntakeBarcode.Clear();
                    TxtIntakeQty.Clear();
                    TxtIntakeCost.Clear();
                    RefreshInventoryGrid();
                }
            }
            catch (Exception ex)
            {
                ShowToast($"فشل التوريد: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Debt Management Logic & PDF/CSV Export

        private void BtnRefreshCustomers_Click(object sender, RoutedEventArgs e)
        {
            RefreshCustomersGrid();
            ShowToast("تم تحديث قائمة العملاء والديون", ToastType.Success);
        }

        private void BtnExportDebtsCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var customers = _debtService.GetAllCustomers();
                string path = ExportService.ExportCustomersToCsv(customers);
                ShowToast($"تم تصدير الديون كملف CSV إلى: {path}", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast($"فشل التصدير: {ex.Message}", ToastType.Error);
            }
        }

        private void BtnExportCustomerPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null)
            {
                ShowToast("يرجى تحديد العميل من الجدول أولاً لإصدار كشف الحساب", ToastType.Warning);
                return;
            }

            try
            {
                var transactions = _debtService.GetCustomerTransactions(_selectedCustomer.CustomerID);
                string path = PdfReportService.GenerateCustomerStatementHtmlPdf(_selectedCustomer, transactions);
                ShowToast($"تم إصد كشف حساب PDF للعميل {_selectedCustomer.CustomerName}!", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast($"فشل إصدار كشف الحساب: {ex.Message}", ToastType.Error);
            }
        }

        private void DgCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgCustomers.SelectedItem is Customer c)
            {
                _selectedCustomer = c;
                TxtSelectedCustInfo.Text = $"العميل المحدد: #{c.CustomerID} {c.CustomerName} (الدين: {c.TotalDebt:N0} YER)";
            }
        }

        private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtCustName.Text.Trim();
            string phone = TxtCustPhone.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ShowToast("يرجى إدخال اسم العميل", ToastType.Warning);
                return;
            }

            if (!decimal.TryParse(TxtCustLimit.Text.Trim(), out decimal limit) || limit < 0)
            {
                ShowToast("يرجى إدخال سقف ائتماني صحيح", ToastType.Warning);
                return;
            }

            var c = new Customer
            {
                CustomerName = name,
                Phone = phone,
                TotalDebt = 0,
                CreditLimit = limit
            };

            if (_debtService.AddCustomer(c))
            {
                ShowToast($"تم تسجيل العميل '{name}' بنجاح!", ToastType.Success);
                TxtCustName.Clear();
                TxtCustPhone.Clear();
                RefreshCustomersGrid();
            }
        }

        private void BtnRecordPayment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null)
            {
                ShowToast("يرجى تحديد العميل من الجدول أولاً", ToastType.Warning);
                return;
            }

            if (!decimal.TryParse(TxtTxAmount.Text.Trim(), out decimal amount) || amount <= 0)
            {
                ShowToast("يرجى إدخال مبلغ سداد صحيح أكبر من الصفر", ToastType.Warning);
                return;
            }

            string notes = TxtTxNotes.Text.Trim();
            try
            {
                if (_debtService.RecordRepayment(_selectedCustomer.CustomerID, amount, notes))
                {
                    ShowToast($"تم تسجيل سداد {amount:N0} YER للعميل {_selectedCustomer.CustomerName}!", ToastType.Success);
                    TxtTxAmount.Clear();
                    RefreshCustomersGrid();
                }
            }
            catch (Exception ex)
            {
                ShowToast($"فشل تسجيل السداد: {ex.Message}", ToastType.Error);
            }
        }

        private void BtnRecordDebt_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null)
            {
                ShowToast("يرجى تحديد العميل من الجدول أولاً", ToastType.Warning);
                return;
            }

            if (!decimal.TryParse(TxtTxAmount.Text.Trim(), out decimal amount) || amount <= 0)
            {
                ShowToast("يرجى إدخال مبلغ صحيح للذمة", ToastType.Warning);
                return;
            }

            string notes = TxtTxNotes.Text.Trim();
            try
            {
                if (_debtService.RecordDebt(_selectedCustomer.CustomerID, amount, notes))
                {
                    ShowToast($"تمت إضافة دين {amount:N0} YER على العميل {_selectedCustomer.CustomerName}!", ToastType.Success);
                    TxtTxAmount.Clear();
                    RefreshCustomersGrid();
                }
            }
            catch (Exception ex)
            {
                ShowToast($"فشل إضافة الدين: {ex.Message}", ToastType.Error);
            }
        }

        #endregion

        #region Analytics Logic & Shift Closure

        private void BtnLoadAnalytics_Click(object sender, RoutedEventArgs e)
        {
            LoadAnalyticsData();
            ShowToast("تم تحميل التقرير المالي لليوم المحدد", ToastType.Success);
        }

        private void BtnCloseShift_Click(object sender, RoutedEventArgs e)
        {
            DateTime targetDate = DpAnalyticsDate.SelectedDate ?? DateTime.Today;
            var confirmResult = MessageBox.Show(
                $"هل تريد حقاً تصفية وإغلاق وردية المبيعات لليوم ({targetDate:yyyy-MM-dd}) وتوثيق الإحصائيات بكشوفات التصفية؟",
                "🔒 إغلاق وتصفية الوردية",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult == MessageBoxResult.Yes)
            {
                try
                {
                    if (_analyticsService.CloseDailyShift(targetDate))
                    {
                        ShowToast($"تمت تصفية وإغلاق وردية اليومية ({targetDate:yyyy-MM-dd}) وتوثيقها بنجاح!", ToastType.Success);
                        LoadAnalyticsData();
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"فشلت تصفية الوردية: {ex.Message}", ToastType.Error);
                }
            }
        }

        private void BtnExportReportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime targetDate = DpAnalyticsDate.SelectedDate ?? DateTime.Today;
                var report = _analyticsService.GetDailyReport(targetDate);
                string path = ExportService.ExportDailyReportToCsv(report);
                ShowToast($"تم تصدير التقرير المالي CSV إلى: {path}", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast($"فشل تصدير التقرير: {ex.Message}", ToastType.Error);
            }
        }

        #endregion
    }
}
