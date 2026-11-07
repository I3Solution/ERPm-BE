using System.Data;
using System.Data.Common;
using Dapper;
using ERPm.Application.Common.Crud;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Features.System;
using ERPm.Application.DTOs.Sales;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

/// <summary>
/// Sales dashboards as set-based SQL (Dapper). Raw SQL skips the EF query filters, so every query repeats the
/// data scope itself: soft delete, then branch, department and own-record rules of the caller.
/// </summary>
public sealed class SalesAnalyticsService(ApplicationDbContext context, IBranchService branches) : ISalesAnalyticsService
{
    private const int MaxDays = 3660;
    private const string PostedInvoice = "i.status = 1";   // VoucherStatus.Posted
    private const string PostedReturn = "r.status = 1";
    private const string LiveOrder = "o.status IN ('Confirmed', 'Processing', 'Completed')";

    public async Task<SalesSummaryDto> GetSummaryAsync(SalesAnalyticsQuery query, CancellationToken ct = default)
    {
        var p = await ParametersAsync(query, ct);
        var sql = $"""
            SELECT
              (SELECT COALESCE(SUM(i.total_amount), 0) FROM "tbl_SALE_Invoice" i
                WHERE {PostedInvoice} AND i.invoice_date >= @from AND i.invoice_date < @to AND {Scope("i")}) AS "InvoiceRevenue",
              (SELECT COALESCE(SUM(ii.cost_amount), 0) FROM "tbl_SALE_InvoiceItem" ii JOIN "tbl_SALE_Invoice" i ON i.id = ii.sales_invoice_id
                WHERE {PostedInvoice} AND i.invoice_date >= @from AND i.invoice_date < @to AND {Scope("i")}) AS "InvoiceCost",
              (SELECT COALESCE(SUM(r.total_refund_amount), 0) FROM "tbl_SD_SalesReturn" r
                WHERE {PostedReturn} AND r.return_date >= @from AND r.return_date < @to AND {Scope("r")}) AS "ReturnRefund",
              (SELECT COALESCE(SUM(r.total_cost_amount), 0) FROM "tbl_SD_SalesReturn" r
                WHERE {PostedReturn} AND r.return_date >= @from AND r.return_date < @to AND {Scope("r")}) AS "ReturnCost",
              (SELECT COUNT(*)::int FROM "tbl_SALE_Order" o WHERE o.order_date >= @from AND o.order_date < @to AND {Scope("o")}) AS "TotalOrders",
              (SELECT COUNT(*)::int FROM "tbl_SALE_Order" o WHERE o.status = 'Completed' AND o.order_date >= @from AND o.order_date < @to AND {Scope("o")}) AS "CompletedOrders",
              (SELECT COUNT(*)::int FROM "tbl_SALE_Order" o WHERE o.status = 'Cancelled' AND o.order_date >= @from AND o.order_date < @to AND {Scope("o")}) AS "CancelledOrders"
            """;
        var row = await QuerySingleAsync<SummaryRow>(sql, p, ct);
        var revenue = row.InvoiceRevenue - row.ReturnRefund;
        var cost = row.InvoiceCost - row.ReturnCost;
        var profit = revenue - cost;
        return new SalesSummaryDto
        {
            TotalRevenue = revenue, TotalCost = cost, GrossProfit = profit,
            GrossMarginPercent = revenue > 0 ? Math.Round(profit / revenue * 100m, 2) : 0m,
            TotalOrders = row.TotalOrders, CompletedOrders = row.CompletedOrders, CancelledOrders = row.CancelledOrders
        };
    }

    public async Task<List<SalespersonKpiDto>> GetSalespersonKpiAsync(SalesAnalyticsQuery query, CancellationToken ct = default)
    {
        var p = await ParametersAsync(query, ct);
        var sql = $"""
            SELECT e.id AS "SalespersonId", e.full_name AS "SalespersonName", d.name AS "DepartmentName",
                   COUNT(*)::int AS "TotalOrders", COALESCE(SUM(o.total_amount), 0) AS "TotalRevenue"
            FROM "tbl_SALE_Order" o
            JOIN "tbl_HRM_Employee" e ON e.id = o.salesperson_id
            LEFT JOIN "tbl_HRM_Department" d ON d.id = e.department_id
            WHERE {LiveOrder} AND o.order_date >= @from AND o.order_date < @to AND {Scope("o")}
            GROUP BY e.id, e.full_name, d.name
            ORDER BY "TotalRevenue" DESC, e.full_name
            """;
        return (await QueryAsync<SalespersonKpiDto>(sql, p, ct)).ToList();
    }

    public async Task<SalesFunnelDto> GetFunnelAsync(SalesAnalyticsQuery query, CancellationToken ct = default)
    {
        var p = await ParametersAsync(query, ct);
        var sql = $"""
            SELECT
              (SELECT COUNT(*)::int FROM "tbl_SD_SalesQuotation" q WHERE q.quotation_date >= @from AND q.quotation_date < @to AND {Scope("q")}) AS "QuotationCount",
              (SELECT COALESCE(SUM(q.total_amount), 0) FROM "tbl_SD_SalesQuotation" q WHERE q.quotation_date >= @from AND q.quotation_date < @to AND {Scope("q")}) AS "QuotationTotalAmount",
              (SELECT COUNT(*)::int FROM "tbl_SD_SalesQuotation" q WHERE q.status = 'Converted' AND q.quotation_date >= @from AND q.quotation_date < @to AND {Scope("q")}) AS "ConvertedQuotations",
              (SELECT COUNT(*)::int FROM "tbl_SALE_Order" o WHERE {LiveOrder} AND o.order_date >= @from AND o.order_date < @to AND {Scope("o")}) AS "OrderCount",
              (SELECT COALESCE(SUM(o.total_amount), 0) FROM "tbl_SALE_Order" o WHERE {LiveOrder} AND o.order_date >= @from AND o.order_date < @to AND {Scope("o")}) AS "OrderTotalAmount",
              (SELECT COUNT(*)::int FROM "tbl_SALE_Invoice" i WHERE {PostedInvoice} AND i.invoice_date >= @from AND i.invoice_date < @to AND {Scope("i")}) AS "InvoiceCount",
              (SELECT COALESCE(SUM(i.total_amount), 0) FROM "tbl_SALE_Invoice" i WHERE {PostedInvoice} AND i.invoice_date >= @from AND i.invoice_date < @to AND {Scope("i")}) AS "InvoiceTotalAmount"
            """;
        var row = await QuerySingleAsync<FunnelRow>(sql, p, ct);
        return new SalesFunnelDto
        {
            QuotationCount = row.QuotationCount, QuotationTotalAmount = row.QuotationTotalAmount,
            OrderCount = row.OrderCount, OrderTotalAmount = row.OrderTotalAmount,
            InvoiceCount = row.InvoiceCount, InvoiceTotalAmount = row.InvoiceTotalAmount,
            ConversionRatePercent = row.QuotationCount > 0 ? Math.Round(row.ConvertedQuotations * 100m / row.QuotationCount, 2) : 0m
        };
    }

    /// <summary>Soft delete, requested branch and the caller's data scope for one table alias.</summary>
    private static string Scope(string a) =>
        $"({a}.is_deleted = false AND (@allBranches OR {a}.branch_id = ANY(@branchFilter)) AND (@unrestricted OR {a}.branch_id = ANY(@branches) " +
        $"OR ({a}.department_id IS NOT NULL AND {a}.department_id = ANY(@departments)) OR (CAST(@own AS text) IS NOT NULL AND {a}.created_by = CAST(@own AS text))))";

    private async Task<DynamicParameters> ParametersAsync(SalesAnalyticsQuery query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var from = query.From ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = query.To ?? now.AddTicks(1);
        Require(from.Kind == DateTimeKind.Utc && to.Kind == DateTimeKind.Utc && from < to && (to - from).TotalDays <= MaxDays, "error.validation.request");

        var filter = query.BranchId is > 0 ? (await branches.GetBranchAndDescendantIdsAsync(query.BranchId.Value, ct)).ToArray() : [];
        Require(query.BranchId is not > 0 || filter.Length > 0, "error.validation.reference");

        var p = new DynamicParameters();
        p.Add("from", from);
        p.Add("to", to);
        p.Add("allBranches", query.BranchId is not > 0);
        p.Add("branchFilter", new IntArray(filter));
        p.Add("unrestricted", context.IsUnrestricted);
        p.Add("branches", new IntArray(context.AllowedBranchIds));
        p.Add("departments", new IntArray(context.AllowedDepartmentIds));
        p.Add("own", context.OwnRecordsUserId, DbType.String);
        return p;
    }

    private async Task<T> QuerySingleAsync<T>(string sql, DynamicParameters p, CancellationToken ct) =>
        await WithConnectionAsync(connection => connection.QuerySingleAsync<T>(new CommandDefinition(sql, p, cancellationToken: ct)), ct);

    private async Task<IEnumerable<T>> QueryAsync<T>(string sql, DynamicParameters p, CancellationToken ct) =>
        await WithConnectionAsync(connection => connection.QueryAsync<T>(new CommandDefinition(sql, p, cancellationToken: ct)), ct);

    // Shares the context's connection; opened here only when EF has not opened it already.
    private async Task<T> WithConnectionAsync<T>(Func<DbConnection, Task<T>> action, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try { return await action(connection); }
        finally { if (opened) await connection.CloseAsync(); }
    }

    /// <summary>Dapper expands collections into lists; a PostgreSQL array parameter has to be passed explicitly.</summary>
    private sealed class IntArray(int[] values) : SqlMapper.ICustomQueryParameter
    {
        public void AddParameter(IDbCommand command, string name) =>
            command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = values });
    }

    private sealed class SummaryRow
    {
        public decimal InvoiceRevenue { get; set; }
        public decimal InvoiceCost { get; set; }
        public decimal ReturnRefund { get; set; }
        public decimal ReturnCost { get; set; }
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
    }

    private sealed class FunnelRow
    {
        public int QuotationCount { get; set; }
        public decimal QuotationTotalAmount { get; set; }
        public int ConvertedQuotations { get; set; }
        public int OrderCount { get; set; }
        public decimal OrderTotalAmount { get; set; }
        public int InvoiceCount { get; set; }
        public decimal InvoiceTotalAmount { get; set; }
    }
}
