using System;
using System.Collections.Generic;
using DukkaniPOS.Models;
using DukkaniPOS.Services;

namespace DukkaniPOS.UI
{
    public class ConsoleUI
    {
        private readonly InventoryService _inventoryService;
        private readonly POSService _posService;
        private readonly DebtService _debtService;
        private readonly AnalyticsService _analyticsService;

        // ANSI Color formatting helpers
        private const string Reset = "\u001b[0m";
        private const string Bold = "\u001b[1m";
        private const string Green = "\u001b[32m";
        private const string Red = "\u001b[31m";
        private const string Yellow = "\u001b[33m";
        private const string Cyan = "\u001b[36m";
        private const string Magenta = "\u001b[35m";
        private const string Blue = "\u001b[34m";

        public ConsoleUI(
            InventoryService inventoryService,
            POSService posService,
            DebtService debtService,
            AnalyticsService analyticsService)
        {
            _inventoryService = inventoryService;
            _posService = posService;
            _debtService = debtService;
            _analyticsService = analyticsService;
        }

        public void Run()
        {
            while (true)
            {
                Console.Clear();
                DrawHeader();
                Console.WriteLine($"{Bold}{Cyan}  1. [💳 POS / Cashier]{Reset} Record New Sale");
                Console.WriteLine($"{Bold}{Yellow}  2. [📦 Inventory Management]{Reset} Add Product / Stock Intake / Alerts");
                Console.WriteLine($"{Bold}{Green}  3. [📊 Daily Analytics Dashboard]{Reset} Revenue, Profit & Stock Warnings");
                Console.WriteLine($"{Bold}{Magenta}  4. [👥 Debt Management]{Reset} Customers, Debts & Repayments");
                Console.WriteLine($"{Bold}{Red}  5. [🚪 Exit System]{Reset}");
                Console.WriteLine(new string('=', 65));
                Console.Write($"{Bold}Select Option [1-5]: {Reset}");

                string input = Console.ReadLine()?.Trim() ?? "";

                try
                {
                    switch (input)
                    {
                        case "1":
                            RunPOSCashierMenu();
                            break;
                        case "2":
                            RunInventoryMenu();
                            break;
                        case "3":
                            RunAnalyticsMenu();
                            break;
                        case "4":
                            RunDebtMenu();
                            break;
                        case "5":
                            Console.WriteLine($"\n{Bold}{Green}Thank you for using DukkaniPOS! Goodbye.{Reset}\n");
                            return;
                        default:
                            ShowError("Invalid selection. Press Enter to try again.");
                            Console.ReadLine();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Unexpected Error: {ex.Message}");
                    Console.WriteLine("\nPress Enter to continue...");
                    Console.ReadLine();
                }
            }
        }

        private void DrawHeader()
        {
            Console.WriteLine($"{Bold}{Cyan}================================================================={Reset}");
            Console.WriteLine($"{Bold}{Cyan}               DUKKANI POS & INVENTORY SYSTEM                    {Reset}");
            Console.WriteLine($"{Bold}{Blue}                   Location: Al-Bayda, Yemen                     {Reset}");
            Console.WriteLine($"{Bold}{Cyan}================================================================={Reset}");
        }

        #region 1. POS Cashier Section

        private void RunPOSCashierMenu()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Cyan}=== 💳 POS CASHIER TERMINAL ==={Reset}\n");

            var cart = new List<CartItem>();

            while (true)
            {
                Console.WriteLine($"{Bold}Scan Product Barcode / ID (or type '{Green}DONE{Reset}' to Checkout, '{Yellow}LIST{Reset}' to View Catalog, '{Red}CANCEL{Reset}' to Abort):{Reset}");
                Console.Write("> ");
                string code = Console.ReadLine()?.Trim() ?? "";

                if (string.Equals(code, "CANCEL", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"{Yellow}Sale cancelled.{Reset}");
                    Pause();
                    return;
                }

                if (string.Equals(code, "DONE", StringComparison.OrdinalIgnoreCase))
                {
                    if (cart.Count == 0)
                    {
                        ShowError("Cart is empty! Scan at least one item.");
                        continue;
                    }
                    break; // Proceed to checkout
                }

                if (string.Equals(code, "LIST", StringComparison.OrdinalIgnoreCase))
                {
                    DisplayProductCatalogQuick();
                    continue;
                }

                var product = _inventoryService.GetProductById(code);
                if (product == null)
                {
                    ShowError($"Product with Barcode/ID '{code}' not found!");
                    continue;
                }

                if (product.StockQuantity <= 0)
                {
                    ShowError($"'{product.ProductName}' is OUT OF STOCK!");
                    continue;
                }

                Console.WriteLine($"Found: {Bold}{Green}{product.ProductName}{Reset} | Price: {product.SellingPrice:N0} YER | In Stock: {product.StockQuantity}");
                int qty = ReadInt($"Enter Quantity [1 - {product.StockQuantity}]: ", 1, product.StockQuantity);

                // Add or update cart
                var existingInCart = cart.Find(c => c.Product.ProductID == product.ProductID);
                if (existingInCart != null)
                {
                    if (existingInCart.Quantity + qty > product.StockQuantity)
                    {
                        ShowError($"Cannot add {qty} more. Total in cart ({existingInCart.Quantity + qty}) exceeds available stock ({product.StockQuantity}).");
                        continue;
                    }
                    existingInCart.Quantity += qty;
                }
                else
                {
                    cart.Add(new CartItem { Product = product, Quantity = qty });
                }

                Console.WriteLine($"{Green}✓ Added {qty} x {product.ProductName} to cart.{Reset}\n");
                DisplayCartSummary(cart);
            }

            // Checkout Process
            Console.Clear();
            Console.WriteLine($"{Bold}{Cyan}=== 🛒 POS CHECKOUT ==={Reset}");
            DisplayCartSummary(cart);

            Console.WriteLine($"\n{Bold}Payment Method:{Reset}");
            Console.WriteLine($"  1. {Green}Cash Payment{Reset}");
            Console.WriteLine($"  2. {Magenta}Debt / Credit Sale (Customer Account){Reset}");
            int payChoice = ReadInt("Select Choice [1-2]: ", 1, 2);

            int? customerId = null;
            string paymentMethodName = "CASH";

            if (payChoice == 2)
            {
                DisplayCustomersQuick();
                customerId = ReadInt("Enter Customer ID for Debt Credit: ", 1, 999999);
                var cust = _debtService.GetCustomerById(customerId.Value);
                if (cust == null)
                {
                    ShowError($"Customer ID #{customerId.Value} does not exist. Sale aborted.");
                    Pause();
                    return;
                }
                paymentMethodName = $"CREDIT DEBT ({cust.CustomerName})";
            }

            try
            {
                _posService.ProcessSale(cart, customerId);
                Console.WriteLine($"\n{Bold}{Green}✔ TRANSACTION SUCCESSFUL! Stock updated.{Reset}\n");
                PrintReceipt(cart, paymentMethodName);
            }
            catch (Exception ex)
            {
                ShowError($"Transaction Failed: {ex.Message}");
            }

            Pause();
        }

        private void DisplayCartSummary(List<CartItem> cart)
        {
            Console.WriteLine($"{Bold}-----------------------------------------------------------------{Reset}");
            Console.WriteLine($"{Bold}{"ITEM",-30} | {"QTY",-5} | {"UNIT PRICE",-12} | {"TOTAL",-12}{Reset}");
            Console.WriteLine($"{Bold}-----------------------------------------------------------------{Reset}");
            decimal grandTotal = 0;
            foreach (var item in cart)
            {
                Console.WriteLine($"{item.Product.ProductName,-30} | {item.Quantity,-5} | {item.Product.SellingPrice,10:N0} YER | {item.TotalAmount,10:N0} YER");
                grandTotal += item.TotalAmount;
            }
            Console.WriteLine($"{Bold}-----------------------------------------------------------------{Reset}");
            Console.WriteLine($"{Bold}{Cyan}GRAND TOTAL: {grandTotal:N0} YER{Reset}");
            Console.WriteLine($"{Bold}-----------------------------------------------------------------{Reset}");
        }

        private void PrintReceipt(List<CartItem> cart, string paymentMethod)
        {
            Console.WriteLine($"{Bold}{Yellow}===================================================={Reset}");
            Console.WriteLine($"{Bold}{Yellow}                 DUKKANI STORES                     {Reset}");
            Console.WriteLine($"{Bold}               Al-Bayda City, Yemen                 ");
            Console.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine($"{"ITEM",-26} {"QTY",-4} {"PRICE",-9} {"TOTAL",-10}");
            Console.WriteLine("----------------------------------------------------");
            decimal total = 0;
            foreach (var item in cart)
            {
                Console.WriteLine($"{Truncate(item.Product.ProductName, 25),-26} {item.Quantity,-4} {item.Product.SellingPrice,7:N0} {item.TotalAmount,9:N0}");
                total += item.TotalAmount;
            }
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine($"{Bold}TOTAL AMOUNT: {total:N0} YER{Reset}");
            Console.WriteLine($"PAYMENT METHOD: {paymentMethod}");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine($"{Green}       Thank you for your business!                 {Reset}");
            Console.WriteLine($"{Bold}{Yellow}===================================================={Reset}\n");
        }

        #endregion

        #region 2. Inventory Management Section

        private void RunInventoryMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"{Bold}{Yellow}=== 📦 INVENTORY MANAGEMENT ==={Reset}\n");
                Console.WriteLine("  1. View All Products");
                Console.WriteLine("  2. Add New Product");
                Console.WriteLine("  3. Stock Intake (Restock Item)");
                Console.WriteLine("  4. View Low Stock Alerts");
                Console.WriteLine("  5. Back to Main Menu");
                Console.Write("\nSelect Choice [1-5]: ");

                string choice = Console.ReadLine()?.Trim() ?? "";
                switch (choice)
                {
                    case "1":
                        ViewAllProducts();
                        break;
                    case "2":
                        AddNewProduct();
                        break;
                    case "3":
                        PerformStockIntake();
                        break;
                    case "4":
                        ViewLowStockAlerts();
                        break;
                    case "5":
                        return;
                    default:
                        ShowError("Invalid choice.");
                        Pause();
                        break;
                }
            }
        }

        private void ViewAllProducts()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Yellow}=== 📦 PRODUCT CATALOG & STOCK ==={Reset}\n");
            var products = _inventoryService.GetAllProducts();

            if (products.Count == 0)
            {
                Console.WriteLine("No products found in database.");
            }
            else
            {
                Console.WriteLine($"{Bold}{"BARCODE",-12} | {"PRODUCT NAME",-30} | {"COST",-10} | {"SELLING",-10} | {"STOCK",-7} | {"STATUS",-10}{Reset}");
                Console.WriteLine(new string('-', 90));
                foreach (var p in products)
                {
                    string status = p.IsLowStock ? $"{Red}⚠️ LOW ({p.StockQuantity}){Reset}" : $"{Green}OK{Reset}";
                    Console.WriteLine($"{p.ProductID,-12} | {Truncate(p.ProductName, 29),-30} | {p.CostPrice,7:N0} YER | {p.SellingPrice,7:N0} YER | {p.StockQuantity,-7} | {status}");
                }
            }
            Pause();
        }

        private void AddNewProduct()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Yellow}=== ➕ ADD NEW PRODUCT ==={Reset}\n");

            string barcode = ReadString("Enter Product Barcode / ID: ");
            if (_inventoryService.GetProductById(barcode) != null)
            {
                ShowError($"Product with Barcode '{barcode}' already exists!");
                Pause();
                return;
            }

            string name = ReadString("Enter Product Name: ");
            decimal cost = ReadDecimal("Enter Unit Cost Price (YER): ");
            decimal selling = ReadDecimal("Enter Unit Selling Price (YER): ");
            int qty = ReadInt("Enter Initial Stock Quantity: ", 0, 100000);
            int minWarn = ReadInt("Enter Low Stock Warning Threshold: ", 1, 1000);

            var p = new Product
            {
                ProductID = barcode,
                ProductName = name,
                CostPrice = cost,
                SellingPrice = selling,
                StockQuantity = qty,
                MinStockWarning = minWarn
            };

            if (_inventoryService.AddProduct(p))
            {
                Console.WriteLine($"\n{Green}✔ Product '{name}' added successfully!{Reset}");
            }
            else
            {
                ShowError("Failed to add product.");
            }
            Pause();
        }

        private void PerformStockIntake()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Yellow}=== 📥 STOCK INTAKE / RESTOCK ==={Reset}\n");
            DisplayProductCatalogQuick();

            string code = ReadString("Enter Product Barcode / ID to Restock: ");
            var p = _inventoryService.GetProductById(code);
            if (p == null)
            {
                ShowError($"Product '{code}' not found.");
                Pause();
                return;
            }

            Console.WriteLine($"Restocking: {Bold}{p.ProductName}{Reset} (Current Stock: {p.StockQuantity}, Cost Price: {p.CostPrice:N0} YER)");
            int qtyAdded = ReadInt("Enter Quantity Added: ", 1, 10000);
            decimal cost = ReadDecimal($"Enter Purchase Unit Cost Price (Press Enter for current {p.CostPrice:N0} YER): ", p.CostPrice);

            try
            {
                if (_inventoryService.StockIntake(code, qtyAdded, cost))
                {
                    Console.WriteLine($"\n{Green}✔ Stock updated! New Stock Quantity: {p.StockQuantity + qtyAdded}{Reset}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Stock intake failed: {ex.Message}");
            }
            Pause();
        }

        private void ViewLowStockAlerts()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Red}=== ⚠️ LOW STOCK WARNING ALERTS ==={Reset}\n");
            var lowStock = _inventoryService.GetLowStockProducts();

            if (lowStock.Count == 0)
            {
                Console.WriteLine($"{Green}Great! All products have healthy stock levels above minimum thresholds.{Reset}");
            }
            else
            {
                Console.WriteLine($"{Bold}{"BARCODE",-12} | {"PRODUCT NAME",-30} | {"CURRENT STOCK",-15} | {"MIN WARNING",-12}{Reset}");
                Console.WriteLine(new string('-', 78));
                foreach (var p in lowStock)
                {
                    Console.WriteLine($"{Red}{p.ProductID,-12} | {Truncate(p.ProductName, 29),-30} | {p.StockQuantity,-15} | {p.MinStockWarning,-12}{Reset}");
                }
            }
            Pause();
        }

        #endregion

        #region 3. Daily Analytics Dashboard

        private void RunAnalyticsMenu()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Green}=== 📊 DAILY FINANCIAL ANALYTICS DASHBOARD ==={Reset}\n");

            Console.Write("Enter Date (yyyy-MM-dd) or press Enter for Today: ");
            string dateInput = Console.ReadLine()?.Trim() ?? "";
            DateTime targetDate = DateTime.Today;
            if (!string.IsNullOrEmpty(dateInput) && DateTime.TryParse(dateInput, out var parsedDate))
            {
                targetDate = parsedDate;
            }

            var report = _analyticsService.GetDailyReport(targetDate);

            Console.WriteLine($"\n{Bold}{Cyan}================================================================={Reset}");
            Console.WriteLine($"{Bold}{Cyan}               FINANCIAL SUMMARY FOR {targetDate:yyyy-MM-dd}                {Reset}");
            Console.WriteLine($"{Bold}{Cyan}================================================================={Reset}");
            Console.WriteLine($"  Total Revenue (Sales)    : {Bold}{Green}{report.TotalRevenue,14:N0} YER{Reset}");
            Console.WriteLine($"  Total Capital Cost       : {Bold}{Yellow}{report.TotalCapitalCost,14:N0} YER{Reset}");
            Console.WriteLine($"  ---------------------------------------------------------------");
            
            string profitColor = report.NetProfit >= 0 ? Green : Red;
            decimal marginStr = report.TotalRevenue > 0 ? (report.NetProfit / report.TotalRevenue) * 100 : 0;

            Console.WriteLine($"  {Bold}NET PROFIT               : {profitColor}{report.NetProfit,14:N0} YER{Reset} (Margin: {marginStr:F1}%)");
            Console.WriteLine($"  Total Sales Transactions : {report.TotalTransactions}");
            Console.WriteLine($"  Total Items Sold         : {report.TotalItemsSold}");
            Console.WriteLine($"{Bold}{Cyan}================================================================={Reset}");

            Console.WriteLine($"\n{Bold}Low Stock Warning Items Count: {report.LowStockProducts.Count}{Reset}");
            if (report.LowStockProducts.Count > 0)
            {
                foreach (var lp in report.LowStockProducts)
                {
                    Console.WriteLine($"  {Red}• [{lp.ProductID}] {lp.ProductName} - Stock: {lp.StockQuantity} (Warning Threshold: {lp.MinStockWarning}){Reset}");
                }
            }

            Pause();
        }

        #endregion

        #region 4. Debt Management Section

        private void RunDebtMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"{Bold}{Magenta}=== 👥 DEBT MANAGEMENT (CUSTOMERS & CREDITS) ==={Reset}\n");
                Console.WriteLine("  1. View All Customers & Debt Status");
                Console.WriteLine("  2. Add New Customer");
                Console.WriteLine("  3. Record Debt / Credit Extension");
                Console.WriteLine("  4. Record Customer Payment / Repayment");
                Console.WriteLine("  5. View Customer Statement History");
                Console.WriteLine("  6. Back to Main Menu");
                Console.Write("\nSelect Choice [1-6]: ");

                string choice = Console.ReadLine()?.Trim() ?? "";
                switch (choice)
                {
                    case "1":
                        ViewAllCustomers();
                        break;
                    case "2":
                        AddNewCustomer();
                        break;
                    case "3":
                        RecordCustomerDebt();
                        break;
                    case "4":
                        RecordCustomerRepayment();
                        break;
                    case "5":
                        ViewCustomerHistory();
                        break;
                    case "6":
                        return;
                    default:
                        ShowError("Invalid choice.");
                        Pause();
                        break;
                }
            }
        }

        private void ViewAllCustomers()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Magenta}=== 👥 CUSTOMER DEBT DIRECTORY ==={Reset}\n");
            var customers = _debtService.GetAllCustomers();

            if (customers.Count == 0)
            {
                Console.WriteLine("No customers recorded.");
            }
            else
            {
                Console.WriteLine($"{Bold}{"ID",-4} | {"CUSTOMER NAME",-25} | {"PHONE",-12} | {"TOTAL DEBT",-12} | {"CREDIT LIMIT",-12} | {"REMAINING",-12}{Reset}");
                Console.WriteLine(new string('-', 88));
                foreach (var c in customers)
                {
                    string debtStr = c.TotalDebt > 0 ? $"{Red}{c.TotalDebt,10:N0} YER{Reset}" : $"{Green}{c.TotalDebt,10:N0} YER{Reset}";
                    Console.WriteLine($"{c.CustomerID,-4} | {Truncate(c.CustomerName, 24),-25} | {c.Phone,-12} | {debtStr} | {c.CreditLimit,10:N0} YER | {c.RemainingCredit,10:N0} YER");
                }
            }
            Pause();
        }

        private void AddNewCustomer()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Magenta}=== ➕ ADD NEW CUSTOMER ==={Reset}\n");

            string name = ReadString("Enter Customer Name: ");
            string phone = ReadString("Enter Customer Phone: ");
            decimal creditLimit = ReadDecimal("Enter Maximum Credit Limit (YER): ");

            var c = new Customer
            {
                CustomerName = name,
                Phone = phone,
                TotalDebt = 0,
                CreditLimit = creditLimit
            };

            if (_debtService.AddCustomer(c))
            {
                Console.WriteLine($"\n{Green}✔ Customer '{name}' added successfully!{Reset}");
            }
            else
            {
                ShowError("Failed to add customer.");
            }
            Pause();
        }

        private void RecordCustomerDebt()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Magenta}=== 📝 RECORD CUSTOMER DEBT ==={Reset}\n");
            DisplayCustomersQuick();

            int id = ReadInt("Enter Customer ID: ", 1, 999999);
            var cust = _debtService.GetCustomerById(id);
            if (cust == null)
            {
                ShowError($"Customer ID #{id} not found.");
                Pause();
                return;
            }

            Console.WriteLine($"Customer: {Bold}{cust.CustomerName}{Reset} | Current Debt: {cust.TotalDebt:N0} YER | Credit Limit: {cust.CreditLimit:N0} YER");
            decimal amount = ReadDecimal("Enter Additional Debt Amount (YER): ");
            string notes = ReadString("Enter Notes/Reason: ", "Manual Debt Entry");

            try
            {
                if (_debtService.RecordDebt(id, amount, notes))
                {
                    Console.WriteLine($"\n{Green}✔ Debt recorded! New Balance: {cust.TotalDebt + amount:N0} YER{Reset}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to record debt: {ex.Message}");
            }
            Pause();
        }

        private void RecordCustomerRepayment()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Magenta}=== 💵 RECORD DEBT PAYMENT / REPAYMENT ==={Reset}\n");
            DisplayCustomersQuick();

            int id = ReadInt("Enter Customer ID: ", 1, 999999);
            var cust = _debtService.GetCustomerById(id);
            if (cust == null)
            {
                ShowError($"Customer ID #{id} not found.");
                Pause();
                return;
            }

            Console.WriteLine($"Customer: {Bold}{cust.CustomerName}{Reset} | Current Debt: {cust.TotalDebt:N0} YER");
            if (cust.TotalDebt <= 0)
            {
                Console.WriteLine($"{Green}Customer has zero outstanding debt!{Reset}");
                Pause();
                return;
            }

            decimal amount = ReadDecimal($"Enter Repayment Amount (YER, max {cust.TotalDebt:N0}): ");
            string notes = ReadString("Enter Payment Notes: ", "Cash Repayment");

            try
            {
                if (_debtService.RecordRepayment(id, amount, notes))
                {
                    decimal newDebt = Math.Max(0, cust.TotalDebt - amount);
                    Console.WriteLine($"\n{Green}✔ Payment received! Remaining Debt: {newDebt:N0} YER{Reset}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Payment processing failed: {ex.Message}");
            }
            Pause();
        }

        private void ViewCustomerHistory()
        {
            Console.Clear();
            Console.WriteLine($"{Bold}{Magenta}=== 📜 CUSTOMER DEBT STATEMENT HISTORY ==={Reset}\n");
            DisplayCustomersQuick();

            int id = ReadInt("Enter Customer ID: ", 1, 999999);
            var cust = _debtService.GetCustomerById(id);
            if (cust == null)
            {
                ShowError($"Customer ID #{id} not found.");
                Pause();
                return;
            }

            var txs = _debtService.GetCustomerTransactions(id);
            Console.WriteLine($"\nStatement for: {Bold}{cust.CustomerName}{Reset} (Phone: {cust.Phone})");
            Console.WriteLine($"Total Debt: {cust.TotalDebt:N0} YER | Credit Limit: {cust.CreditLimit:N0} YER\n");

            if (txs.Count == 0)
            {
                Console.WriteLine("No transactions found for this customer.");
            }
            else
            {
                Console.WriteLine($"{Bold}{"DATE",-20} | {"TYPE",-10} | {"AMOUNT",-12} | {"NOTES",-30}{Reset}");
                Console.WriteLine(new string('-', 78));
                foreach (var t in txs)
                {
                    string typeStr = t.Type == "DEBT" ? $"{Red}DEBT (+){Reset}" : $"{Green}PAYMENT (-){Reset}";
                    Console.WriteLine($"{t.TransactionDate:yyyy-MM-dd HH:mm:ss} | {typeStr,-10} | {t.Amount,10:N0} YER | {t.Notes}");
                }
            }
            Pause();
        }

        #endregion

        #region Helper Utilities

        private void DisplayProductCatalogQuick()
        {
            var products = _inventoryService.GetAllProducts();
            Console.WriteLine($"\n{Bold}--- Quick Catalog ---{Reset}");
            foreach (var p in products)
            {
                Console.WriteLine($"  Code: [{Bold}{p.ProductID}{Reset}] {p.ProductName} | Price: {p.SellingPrice:N0} YER | Stock: {p.StockQuantity}");
            }
            Console.WriteLine();
        }

        private void DisplayCustomersQuick()
        {
            var customers = _debtService.GetAllCustomers();
            Console.WriteLine($"\n{Bold}--- Customers List ---{Reset}");
            foreach (var c in customers)
            {
                Console.WriteLine($"  ID: #{c.CustomerID} | {c.CustomerName} | Debt: {c.TotalDebt:N0} YER | Credit Limit: {c.CreditLimit:N0} YER");
            }
            Console.WriteLine();
        }

        private static string ReadString(string prompt, string defaultValue = "")
        {
            while (true)
            {
                Console.Write(prompt);
                string val = Console.ReadLine()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(val)) return val;
                if (!string.IsNullOrEmpty(defaultValue)) return defaultValue;
                ShowError("Input cannot be empty.");
            }
        }

        private static int ReadInt(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine()?.Trim() ?? "";
                if (int.TryParse(input, out int res) && res >= min && res <= max)
                {
                    return res;
                }
                ShowError($"Please enter a valid integer between {min} and {max}.");
            }
        }

        private static decimal ReadDecimal(string prompt, decimal? defaultValue = null)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine()?.Trim() ?? "";
                if (string.IsNullOrEmpty(input) && defaultValue.HasValue)
                {
                    return defaultValue.Value;
                }
                if (decimal.TryParse(input, out decimal res) && res >= 0)
                {
                    return res;
                }
                ShowError("Please enter a valid non-negative amount.");
            }
        }

        private static void ShowError(string msg)
        {
            Console.WriteLine($"{Bold}{Red}❌ {msg}{Reset}");
        }

        private static void Pause()
        {
            Console.WriteLine($"\nPress {Bold}Enter{Reset} to continue...");
            Console.ReadLine();
        }

        private static string Truncate(string val, int maxLen)
        {
            if (string.IsNullOrEmpty(val)) return "";
            return val.Length <= maxLen ? val : val.Substring(0, maxLen - 3) + "...";
        }

        #endregion
    }
}
