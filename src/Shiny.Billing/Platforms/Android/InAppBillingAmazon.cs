#if ANDROID
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Com.Amazon.Device.Drm;
using Com.Amazon.Device.Drm.Model;
using Com.Amazon.Device.Iap;
using Com.Amazon.Device.Iap.Model;
using Microsoft.Extensions.Logging;
using RequestId = Com.Amazon.Device.Iap.Model.RequestId;

namespace Shiny;

public class InAppBillingAmazon : Java.Lang.Object, IInAppBilling, IPurchasingListener, ILicensingListener
{
    public bool IsConnected { get; set; }
    public bool InTestingMode { get; set; }
    public bool IgnoreInvalidProducts { get; set; }
    public Storefront Storefront { get; }
    public string ReceiptData { get; }
    public bool CanMakePayments { get; }
    
    private readonly ILogger<InAppBillingAmazon> _logger;
    private readonly AndroidPlatform _platform;
    private RequestId? _requestId;
    private TaskCompletionSource<IList<Receipt>>? _getPurchasesCompletionSource;
    private TaskCompletionSource<InAppBillingPurchase>? _purchaseCompletionSource;

    public InAppBillingAmazon(ILogger<InAppBillingAmazon> logger, AndroidPlatform platform)
    {
        _logger = logger;
        _platform = platform;

        LicensingService.VerifyLicense(_platform.CurrentActivity, this);
        PurchasingService.RegisterListener(_platform.CurrentActivity, this);
    }
    
    public void OnPurchaseResponse(PurchaseResponse? response)
    {
        try
        {
            if (response == null)
                return;
            
            if (response.RequestId?.ToString() != _requestId?.ToString())
                return;

            var status = response.GetRequestStatus();
            if (status != PurchaseResponse.RequestStatus.Successful &&
                status != PurchaseResponse.RequestStatus.AlreadyPurchased)
                throw new InAppBillingPurchaseException(PurchaseError.GeneralError,
                    $"Purchase request failed with response status: {status}");

            if (status != PurchaseResponse.RequestStatus.AlreadyPurchased &&
                !string.IsNullOrEmpty(response.Receipt?.ReceiptId))
                PurchasingService.NotifyFulfillment(response.Receipt.ReceiptId, FulfillmentResult.Fulfilled);

            var purchase = Map(response);
            _purchaseCompletionSource?.TrySetResult(purchase);
        }
        catch (Exception ex)
        {
            _purchaseCompletionSource?.TrySetException(ex);
        }
    }
    
    public void OnPurchaseUpdatesResponse(PurchaseUpdatesResponse? response)
    {
        try
        {
            if (response == null)
                return;
            
            if (response.RequestId?.ToString() != _requestId?.ToString())
                return;

            var status = response.GetRequestStatus();
            if (status != PurchaseUpdatesResponse.RequestStatus.Successful)
                throw new Exception($"Get purchases request failed with response status: {status}");

            var receipts = response.Receipts;
            if (receipts == null || receipts.Count == 0)
            {
                _getPurchasesCompletionSource?.TrySetResult(new List<Receipt>());
                return;
            }
            
            try
            {
                foreach (var receipt in receipts)
                    PurchasingService.NotifyFulfillment(receipt.ReceiptId, FulfillmentResult.Fulfilled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying fulfillment for receipts");
            }
            _getPurchasesCompletionSource?.TrySetResult(receipts);
        }
        catch (Exception ex)
        {
            _getPurchasesCompletionSource?.TrySetException(ex);
        }
    }
    
    public async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, CancellationToken cancellationToken = default)
    {
        _getPurchasesCompletionSource = new TaskCompletionSource<IList<Receipt>>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => _getPurchasesCompletionSource.TrySetCanceled(), false);

        _requestId = new RequestId();
        _requestId = PurchasingService.GetPurchaseUpdates(true);
        
        var purchases = await _getPurchasesCompletionSource.Task;
        
        return Map(purchases);
    }

    public async Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string? obfuscatedAccountId = null, string? obfuscatedProfileId = null, string? subOfferToken = null, CancellationToken cancellationToken = default)
    {
        _purchaseCompletionSource = new TaskCompletionSource<InAppBillingPurchase>();

        _requestId = new RequestId();
        _requestId = PurchasingService.Purchase(productId);
        
        var purchase = await _purchaseCompletionSource.Task;
        return purchase;
    }
    public void OnUserDataResponse(UserDataResponse? response) { }
    
    public void PresentCodeRedemption() { }
    public void OnLicenseCommandResponse(LicenseResponse? response) { }
    public void OnProductDataResponse(ProductDataResponse? response) { }

    public Task<IEnumerable<InAppBillingPurchase>> GetPurchasesHistoryAsync(ItemType itemType, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    
    public Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> ConsumePurchaseAsync(string productId, string transactionIdentifier, int quantity, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
    
    public Task<IEnumerable<(string Id, bool Success)>> FinalizePurchaseAsync(string[] transactionIdentifier, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<(string Id, bool Success)>> FinalizePurchaseOfProductAsync(string[] productIds, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> ConnectAsync(bool enablePendingPurchases = true, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, string[] productIds, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    
    #region Mapper
    private static IEnumerable<InAppBillingPurchase> Map(IEnumerable<Receipt> purchaseReceipts)
    {
        var purchases = new List<InAppBillingPurchase>();
        foreach (var purchase in purchaseReceipts)
        {
            var purchaseHistory = new InAppBillingPurchase
            {
                ProductId = purchase.Sku,
                PurchaseToken = purchase.ReceiptId
            };

            if (purchase.PurchaseDate?.Time > 0)
                purchaseHistory.TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(purchase.PurchaseDate.Time).DateTime;

            purchaseHistory.Payload = null;
            if (purchase.CancelDate?.Time > 0)
            {
                purchaseHistory.TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(purchase.CancelDate.Time).DateTime;
                purchaseHistory.State = PurchaseState.Canceled;

            }
            else
                purchaseHistory.State = PurchaseState.Purchased;

            purchases.Add(purchaseHistory);
        }
        return purchases;
    }

    private static InAppBillingPurchase Map(PurchaseResponse? args)
    {
        var purchaseResult = new InAppBillingPurchase();

        if (args?.Receipt == null || args.UserData == null)
            return purchaseResult;

        purchaseResult.PurchaseToken = args.Receipt.ReceiptId;
        if (args.Receipt.CancelDate?.Time > 0)
            purchaseResult.TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(args.Receipt.CancelDate.Time).DateTime;
        if (args.Receipt.PurchaseDate?.Time > 0)
            purchaseResult.TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(args.Receipt.PurchaseDate.Time).DateTime;

        purchaseResult.ProductId = args.Receipt.Sku;
        purchaseResult.State = args.GetRequestStatus() == PurchaseResponse.RequestStatus.Successful ? PurchaseState.Purchased : PurchaseState.Canceled;

        return purchaseResult;

    }
    #endregion
}
#endif