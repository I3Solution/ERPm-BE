using ERPm.Domain.Entities.System;
using ERPm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Provisions standard enterprise notification templates with bilingual translations.
/// </summary>
public static class NotificationTemplateSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        var existingCodes = await context.NotificationTemplates
            .IgnoreQueryFilters()
            .Select(t => t.Code)
            .ToHashSetAsync();

        var templates = new List<NotificationTemplate>
        {
            new()
            {
                Code = "WMS_LOW_STOCK",
                Name = "Cảnh báo tồn kho an toàn",
                Category = NotificationCategory.WMS,
                DefaultSeverity = NotificationSeverity.WARNING,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile, NotificationChannel.Email],
                TitleTemplate = "Cảnh báo tồn kho: {item_name}",
                BodyTemplate = "Sản phẩm {item_name} ({item_code}) tại kho {warehouse_name} chỉ còn {current_stock} {uom}, dưới mức an toàn tối thiểu ({min_stock} {uom}).",
                ActionUrlPattern = "/inventory?search={item_code}",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Cảnh báo tồn kho: {item_name}",
                        Body = "Sản phẩm {item_name} ({item_code}) tại kho {warehouse_name} chỉ còn {current_stock} {uom}, dưới mức an toàn tối thiểu ({min_stock} {uom})."
                    },
                    ["en"] = new()
                    {
                        Title = "Low stock alert: {item_name}",
                        Body = "Product {item_name} ({item_code}) in warehouse {warehouse_name} has {current_stock} {uom} left, below the safety threshold ({min_stock} {uom})."
                    }
                }
            },
            new()
            {
                Code = "SD_ORDER_NEW",
                Name = "Đơn đặt hàng mới",
                Category = NotificationCategory.SD,
                DefaultSeverity = NotificationSeverity.INFO,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile],
                TitleTemplate = "Đơn hàng mới: {order_code}",
                BodyTemplate = "Khách hàng {customer_name} vừa đặt đơn hàng {order_code} với tổng giá trị {total_amount} đ.",
                ActionUrlPattern = "/sales/pos",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Đơn hàng mới: {order_code}",
                        Body = "Khách hàng {customer_name} vừa đặt đơn hàng {order_code} với tổng giá trị {total_amount} đ."
                    },
                    ["en"] = new()
                    {
                        Title = "New sales order: {order_code}",
                        Body = "Customer {customer_name} placed order {order_code} with total value of {total_amount}."
                    }
                }
            },
            new()
            {
                Code = "SD_ORDER_CREDIT_PENDING",
                Name = "Đơn hàng chờ duyệt công nợ",
                Category = NotificationCategory.SD,
                DefaultSeverity = NotificationSeverity.WARNING,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile],
                TitleTemplate = "Đơn hàng {order_code} chờ duyệt công nợ",
                BodyTemplate = "Khách hàng {customer_name} vượt hạn mức công nợ với đơn {order_code} trị giá {total_amount} đ. Cần người có quyền duyệt công nợ xem xét.",
                ActionUrlPattern = "/sales/orders",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Đơn hàng {order_code} chờ duyệt công nợ",
                        Body = "Khách hàng {customer_name} vượt hạn mức công nợ với đơn {order_code} trị giá {total_amount} đ. Cần người có quyền duyệt công nợ xem xét."
                    },
                    ["en"] = new()
                    {
                        Title = "Order {order_code} awaits credit approval",
                        Body = "Customer {customer_name} exceeds the credit limit with order {order_code} worth {total_amount}. A credit approver must review it."
                    }
                }
            },
            new()
            {
                Code = "SD_ORDER_CREDIT_APPROVED",
                Name = "Đơn hàng đã được duyệt công nợ",
                Category = NotificationCategory.SD,
                DefaultSeverity = NotificationSeverity.INFO,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile],
                TitleTemplate = "Đơn hàng {order_code} đã được duyệt công nợ",
                BodyTemplate = "Đơn {order_code} của khách hàng {customer_name} đã được duyệt vượt hạn mức. Lý do: {reason}.",
                ActionUrlPattern = "/sales/orders",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Đơn hàng {order_code} đã được duyệt công nợ",
                        Body = "Đơn {order_code} của khách hàng {customer_name} đã được duyệt vượt hạn mức. Lý do: {reason}."
                    },
                    ["en"] = new()
                    {
                        Title = "Order {order_code} credit approved",
                        Body = "Order {order_code} of customer {customer_name} was approved beyond the credit limit. Reason: {reason}."
                    }
                }
            },
            new()
            {
                Code = "SD_ORDER_CANCELLED",
                Name = "Đơn hàng bị hủy",
                Category = NotificationCategory.SD,
                DefaultSeverity = NotificationSeverity.WARNING,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile],
                TitleTemplate = "Đơn hàng {order_code} đã bị hủy",
                BodyTemplate = "Đơn {order_code} của khách hàng {customer_name} đã bị hủy và tồn kho giữ chỗ được nhả. Lý do: {reason}.",
                ActionUrlPattern = "/sales/orders",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Đơn hàng {order_code} đã bị hủy",
                        Body = "Đơn {order_code} của khách hàng {customer_name} đã bị hủy và tồn kho giữ chỗ được nhả. Lý do: {reason}."
                    },
                    ["en"] = new()
                    {
                        Title = "Order {order_code} cancelled",
                        Body = "Order {order_code} of customer {customer_name} was cancelled and its reserved stock released. Reason: {reason}."
                    }
                }
            },
            new()
            {
                Code = "SD_RETURN_POSTED",
                Name = "Phiếu trả hàng bán đã ghi sổ",
                Category = NotificationCategory.SD,
                DefaultSeverity = NotificationSeverity.INFO,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile],
                TitleTemplate = "Trả hàng bán: {return_code}",
                BodyTemplate = "Khách hàng {customer_name} đã trả hàng theo phiếu {return_code}, số tiền hoàn {total_refund} đ. Hàng đã nhập lại kho.",
                ActionUrlPattern = "/sales/returns",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Trả hàng bán: {return_code}",
                        Body = "Khách hàng {customer_name} đã trả hàng theo phiếu {return_code}, số tiền hoàn {total_refund} đ. Hàng đã nhập lại kho."
                    },
                    ["en"] = new()
                    {
                        Title = "Sales return: {return_code}",
                        Body = "Customer {customer_name} returned goods on {return_code}; refund {total_refund}. The goods are back in stock."
                    }
                }
            },
            new()
            {
                Code = "SD_ORDER_INVOICED",
                Name = "Đơn hàng đã xuất hóa đơn",
                Category = NotificationCategory.SD,
                DefaultSeverity = NotificationSeverity.INFO,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile],
                TitleTemplate = "Đơn hàng {order_code} đã xuất hóa đơn",
                BodyTemplate = "Đơn {order_code} đã xuất hóa đơn {invoice_code}, tổng tiền {total_amount} đ.",
                ActionUrlPattern = "/sales/invoices",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Đơn hàng {order_code} đã xuất hóa đơn",
                        Body = "Đơn {order_code} đã xuất hóa đơn {invoice_code}, tổng tiền {total_amount} đ."
                    },
                    ["en"] = new()
                    {
                        Title = "Order {order_code} invoiced",
                        Body = "Order {order_code} was invoiced as {invoice_code}, total {total_amount}."
                    }
                }
            },
            new()
            {
                Code = "SD_INVOICE_CANCELLED",
                Name = "Cảnh báo hủy hóa đơn",
                Category = NotificationCategory.SD,
                DefaultSeverity = NotificationSeverity.DANGER,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile, NotificationChannel.Email],
                TitleTemplate = "Hủy hóa đơn bán hàng: {invoice_code}",
                BodyTemplate = "Hóa đơn {invoice_code} trị giá {total_amount} đ đã bị hủy bởi {cancelled_by}. Bút toán đảo đã được tự động ghi nhận.",
                ActionUrlPattern = "/sales/invoices",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Hủy hóa đơn bán hàng: {invoice_code}",
                        Body = "Hóa đơn {invoice_code} trị giá {total_amount} đ đã bị hủy bởi {cancelled_by}. Bút toán đảo đã được tự động ghi nhận."
                    },
                    ["en"] = new()
                    {
                        Title = "Sales invoice cancelled: {invoice_code}",
                        Body = "Invoice {invoice_code} amounting to {total_amount} was cancelled by {cancelled_by}. Reversal journal entries posted."
                    }
                }
            },
            new()
            {
                Code = "SYS_ROLE_CHANGED",
                Name = "Thay đổi phân quyền tài khoản",
                Category = NotificationCategory.SYS,
                DefaultSeverity = NotificationSeverity.INFO,
                SupportedChannels = [NotificationChannel.InApp],
                TitleTemplate = "Cập nhật quyền hạn tài khoản",
                BodyTemplate = "Quyền hạn của bạn vừa được cập nhật với vai trò mới: {role_name}.",
                ActionUrlPattern = "/auth/me",
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "Cập nhật quyền hạn tài khoản",
                        Body = "Quyền hạn của bạn vừa được cập nhật với vai trò mới: {role_name}."
                    },
                    ["en"] = new()
                    {
                        Title = "Account permissions updated",
                        Body = "Your account permissions have been updated with role: {role_name}."
                    }
                }
            },
            new()
            {
                Code = "SYS_BROADCAST",
                Name = "Thông báo toàn hệ thống",
                Category = NotificationCategory.SYS,
                DefaultSeverity = NotificationSeverity.INFO,
                SupportedChannels = [NotificationChannel.InApp, NotificationChannel.PushMobile],
                TitleTemplate = "{title}",
                BodyTemplate = "{body}",
                ActionUrlPattern = null,
                Translations = new Dictionary<string, NotificationTemplateTranslation>
                {
                    ["vi"] = new()
                    {
                        Title = "{title}",
                        Body = "{body}"
                    },
                    ["en"] = new()
                    {
                        Title = "{title}",
                        Body = "{body}"
                    }
                }
            }
        };

        foreach (var t in templates)
        {
            if (!existingCodes.Contains(t.Code))
            {
                context.NotificationTemplates.Add(t);
            }
        }

        await context.SaveChangesAsync();
    }
}
