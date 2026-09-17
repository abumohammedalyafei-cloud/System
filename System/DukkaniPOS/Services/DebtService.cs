using System;
using System.Collections.Generic;
using System.Data.SQLite;
using DukkaniPOS.Data;
using DukkaniPOS.Models;

namespace DukkaniPOS.Services
{
    public class DebtService
    {
        public bool AddCustomer(Customer customer)
        {
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Customers (CustomerName, Phone, TotalDebt, CreditLimit)
                VALUES (@name, @phone, @debt, @limit);";

            cmd.Parameters.AddWithValue("@name", customer.CustomerName);
            cmd.Parameters.AddWithValue("@phone", customer.Phone);
            cmd.Parameters.AddWithValue("@debt", customer.TotalDebt);
            cmd.Parameters.AddWithValue("@limit", customer.CreditLimit);

            return cmd.ExecuteNonQuery() > 0;
        }

        public Customer? GetCustomerById(int customerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CustomerID, CustomerName, Phone, TotalDebt, CreditLimit FROM Customers WHERE CustomerID = @id;";
            cmd.Parameters.AddWithValue("@id", customerId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return MapCustomer(reader);
            }
            return null;
        }

        public List<Customer> GetAllCustomers()
        {
            var list = new List<Customer>();
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CustomerID, CustomerName, Phone, TotalDebt, CreditLimit FROM Customers ORDER BY CustomerName;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(MapCustomer(reader));
            }
            return list;
        }

        public bool RecordDebt(int customerId, decimal amount, string notes)
        {
            if (amount <= 0)
                throw new ArgumentException("Debt amount must be positive.");

            using var conn = DatabaseHelper.GetConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                // Verify Customer Credit Limit
                var customer = GetCustomerByIdInternal(conn, trans, customerId);
                if (customer == null)
                    throw new InvalidOperationException($"Customer ID #{customerId} not found.");

                if (customer.TotalDebt + amount > customer.CreditLimit)
                {
                    throw new InvalidOperationException(
                        $"Transaction rejected! Amount ({amount:N0} YER) exceeds credit limit. Current Debt: {customer.TotalDebt:N0} YER, Credit Limit: {customer.CreditLimit:N0} YER.");
                }

                // Update TotalDebt
                using (var updateCmd = conn.CreateCommand())
                {
                    updateCmd.Transaction = trans;
                    updateCmd.CommandText = "UPDATE Customers SET TotalDebt = TotalDebt + @amt WHERE CustomerID = @id;";
                    updateCmd.Parameters.AddWithValue("@amt", amount);
                    updateCmd.Parameters.AddWithValue("@id", customerId);
                    updateCmd.ExecuteNonQuery();
                }

                // Log Debt Transaction
                using (var logCmd = conn.CreateCommand())
                {
                    logCmd.Transaction = trans;
                    logCmd.CommandText = @"
                        INSERT INTO DebtTransactions (CustomerID, Type, Amount, TransactionDate, Notes)
                        VALUES (@cid, 'DEBT', @amt, @date, @notes);";
                    logCmd.Parameters.AddWithValue("@cid", customerId);
                    logCmd.Parameters.AddWithValue("@amt", amount);
                    logCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    logCmd.Parameters.AddWithValue("@notes", notes ?? "New Debt Added");
                    logCmd.ExecuteNonQuery();
                }

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        public bool RecordRepayment(int customerId, decimal amount, string notes)
        {
            if (amount <= 0)
                throw new ArgumentException("Repayment amount must be positive.");

            using var conn = DatabaseHelper.GetConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                var customer = GetCustomerByIdInternal(conn, trans, customerId);
                if (customer == null)
                    throw new InvalidOperationException($"Customer ID #{customerId} not found.");

                // Update TotalDebt
                using (var updateCmd = conn.CreateCommand())
                {
                    updateCmd.Transaction = trans;
                    updateCmd.CommandText = "UPDATE Customers SET TotalDebt = MAX(0, TotalDebt - @amt) WHERE CustomerID = @id;";
                    updateCmd.Parameters.AddWithValue("@amt", amount);
                    updateCmd.Parameters.AddWithValue("@id", customerId);
                    updateCmd.ExecuteNonQuery();
                }

                // Log Repayment Transaction
                using (var logCmd = conn.CreateCommand())
                {
                    logCmd.Transaction = trans;
                    logCmd.CommandText = @"
                        INSERT INTO DebtTransactions (CustomerID, Type, Amount, TransactionDate, Notes)
                        VALUES (@cid, 'REPAYMENT', @amt, @date, @notes);";
                    logCmd.Parameters.AddWithValue("@cid", customerId);
                    logCmd.Parameters.AddWithValue("@amt", amount);
                    logCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    logCmd.Parameters.AddWithValue("@notes", notes ?? "Debt Repayment");
                    logCmd.ExecuteNonQuery();
                }

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        public List<DebtTransaction> GetCustomerTransactions(int customerId)
        {
            var list = new List<DebtTransaction>();
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TransactionID, CustomerID, Type, Amount, TransactionDate, Notes 
                FROM DebtTransactions 
                WHERE CustomerID = @cid 
                ORDER BY TransactionDate DESC;";
            cmd.Parameters.AddWithValue("@cid", customerId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DebtTransaction
                {
                    TransactionID = reader.GetInt64(0),
                    CustomerID = reader.GetInt32(1),
                    Type = reader.GetString(2),
                    Amount = Convert.ToDecimal(reader.GetDouble(3)),
                    TransactionDate = DateTime.Parse(reader.GetString(4)),
                    Notes = reader.IsDBNull(5) ? "" : reader.GetString(5)
                });
            }
            return list;
        }

        private Customer? GetCustomerByIdInternal(SQLiteConnection conn, SQLiteTransaction trans, int customerId)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "SELECT CustomerID, CustomerName, Phone, TotalDebt, CreditLimit FROM Customers WHERE CustomerID = @id;";
            cmd.Parameters.AddWithValue("@id", customerId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return MapCustomer(reader);
            }
            return null;
        }

        private static Customer MapCustomer(SQLiteDataReader reader)
        {
            return new Customer
            {
                CustomerID = reader.GetInt32(0),
                CustomerName = reader.GetString(1),
                Phone = reader.GetString(2),
                TotalDebt = Convert.ToDecimal(reader.GetDouble(3)),
                CreditLimit = Convert.ToDecimal(reader.GetDouble(4))
            };
        }
    }
}
