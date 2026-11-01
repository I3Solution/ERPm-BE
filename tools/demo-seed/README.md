# Demo data loader

`seed_demo.py` fills a **running** ERPm API with one fictional company so everybody on the team can look at the same
demo environment. It calls only the public API (the same endpoints the screens use), so it stays valid when the
database schema changes and needs no SQL.

## What it creates

| Kind | Content |
| :--- | :--- |
| Organization | Company A under the group root › branches Hà Nội (with store 01) and TP.HCM |
| Departments | Khối Tài chính › Phòng Kế toán › Bộ phận Công nợ; Phòng Kinh doanh |
| Job positions | Kế toán trưởng, Kế toán bán hàng, Giám đốc chi nhánh (each with its default role), Nhân viên bán hàng |
| Department role | Phòng Kinh doanh grants the sales staff role to everyone below it |
| People | 5 employees (NV001–NV005), accounts `ketoan` and `banhang` linked to NV001 and NV004 |
| Items | 3 sample items (SP001–SP003) |

## Run

1. Start an API on an empty database with a bootstrap administrator (`BootstrapAdmin__Password`, `Database__AutoMigrate=true`).
2. Run the loader:

```
set ERPM_ADMIN_PASSWORD=<administrator password>
python tools/demo-seed/seed_demo.py --url http://127.0.0.1:5143 --out demo-credentials.local.json
```

- `ERPM_DEMO_PASSWORD` (optional) sets the password of the demo accounts; otherwise one is generated and written to
  `--out` (or printed once when `--out` is not given). Name the file `*.local.json`: git ignores it. **Never commit it.**
- A second run stops before changing anything ("Company A already exists").
- Python 3.9+ standard library only; nothing to install.

## Rules

Data is fictional: no real customer, tax code or person. Keep it that way when extending the script.
