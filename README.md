# Battery Statusbar · 桌面电量状态条

Windows 桌面悬浮电量条，毛玻璃圆角 + 低饱和配色，C# / WinForms（.NET 8）单进程实现，目标：**特别轻量**（框架依赖单文件约 180KB，空闲时 CPU 占用近 0）。

## 功能

- 笔记本 / HID 键鼠 / 蓝牙耳机 电量同屏显示，按电量排序
- 蓝牙耳机电量：Windows PnP 层 HFP 电池属性（`{104EA319-6EE2-4701-BD47-8DDBF425BBE5},2`，与系统"设置"页同源）+ BLE GATT Battery Service (0x180F / 0x2A19)
- 设备断开自动从列表消失、连上自动出现，窗口高度随行数自适应
- 电量 <30% 时该行进度条与百分比变暗红提示
- 毛玻璃背景实时取桌面壁纸（PrintWindow + box blur 外扩消除边缘伪影）
- 拖动移动（移动中实时居中吸附）、右下角拖拽缩放
- 托盘菜单：显示/隐藏、刷新、有窗口时自动隐藏、开机自启、退出
- 自适应刷新间隔：电量 ≥90% → 10 分钟；≥60% → 3 分钟；<60% → 1 分钟
- Win+D 不消失（看门狗检测恢复）

## 构建

需要 .NET 8 SDK：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

产物：`bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/StatusBar.exe`

## 技术要点

- `UpdateLayeredWindow` + 32bpp 预乘 Alpha 分层窗口（无边框、逐像素透明）
- SetupAPI 枚举 HID 设备路径，P/Invoke 直调 hid.dll 读取厂商电量协议
- WinRT `DeviceInformation`（TFM 必须 `net8.0-windows10.0.19041.0`）枚举蓝牙 AEP / PnP 设备
- `EmptyWorkingSet` 定期裁剪工作集

## 已适配设备

| 设备 | 电量来源 |
|---|---|
| 笔记本电池 | GetSystemPowerStatus |
| 迈从 K87S 键盘 | 厂商 HID Feature 协议 |
| 雷柏 VT3 MAX 鼠标 | 厂商 HID 报文协议 |
| HUAWEI FreeBuds Pro 5 | PnP 层 HFP 电池属性 |
| 其他 BLE 设备 | GATT Battery Service |

> 蓝牙设备需向 Windows 标准协议上报电量，仅厂商私有 APP 可读电量的设备不显示。HFP 只提供汇总电量，充电仓/单耳电量需厂商私有协议（如华为电脑管家的私有通道），不在本项目范围内。
