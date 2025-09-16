using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class RequestedCisItem : ObservableObject
{
    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private string requestedCis;

    [ObservableProperty]
    private string productName;

    [ObservableProperty]
    private string status;

    [ObservableProperty]
    private string n1;

    [ObservableProperty]
    private string n2;

    [ObservableProperty]
    private string n3;

    // ✅ Добавлено: для логики продажи
    [ObservableProperty]
    private bool isProcessed;

    // ✅ Добавлено: автоматически возвращает подготовленный код
    public string Value => RequestedCis?.Replace("", "\u001d");

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string ownerName;


}

