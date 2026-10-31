using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels;

public sealed class OrderEditViewModel : OrderCreateViewModel
{
    public long Id { get; set; }

    public string? CurrentSampleImagePath { get; set; }

    public string? CurrentPrintImagePath { get; set; }
}