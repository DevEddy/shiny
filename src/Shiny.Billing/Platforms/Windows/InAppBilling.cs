
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.UI.Xaml;
using Windows.Services.Store;
using Microsoft.Maui.Platform;

namespace Shiny
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public partial class InAppBilling : IInAppBilling
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public InAppBilling()
        {
        }
        /// <summary>
        /// Returns the active windows
        /// </summary>
        public static Func<Window> GetActiveWindow { get; set; }

        StoreContext GetStoreContext()
        {
            var window = GetActiveWindow?.Invoke();
            if(window is null)
                throw new NullReferenceException("GetActiveWindow returned null");
            
            var handle = window.GetWindowHandle();
            
            var context = StoreContext.GetDefault();
            WinRT.Interop.InitializeWithWindow.Initialize(context, handle);
            
            return context;
        }


        public bool InTestingMode { get; set; } = false;
        
        public async Task<bool> ConsumePurchaseAsync(string productId, string transactionIdentifier, int quantity, CancellationToken cancellationToken = default)
        {
            var context = GetStoreContext();
            
            var trackingId = Guid.NewGuid();
            var result = await context.ReportConsumableFulfillmentAsync(productId, (uint)quantity, trackingId);
            switch (result.Status)
            {
                case StoreConsumableStatus.InsufficentQuantity:
                    throw new InAppBillingPurchaseException(PurchaseError.InsufficentQuantity, result.ExtendedError?.Message);
                case StoreConsumableStatus.Succeeded:
                    {
                        return true;
                    }
                case StoreConsumableStatus.NetworkError:
                    throw new InAppBillingPurchaseException(PurchaseError.ProductRequestFailed, result.ExtendedError?.Message);
                case StoreConsumableStatus.ServerError:
                    throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, result.ExtendedError?.Message);
                default:
                    throw new InAppBillingPurchaseException(PurchaseError.GeneralError, result.ExtendedError?.Message);
            }
        }
        public async Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, string[] productIds, CancellationToken cancellationToken = default)
        {
            var context = GetStoreContext();
            var results = await context.GetStoreProductsAsync(itemType.ToProductFilter(), productIds);
            return (from item in results.Products
                    select item.Value.ToInAppBillingProduct()).ToList();
        }
        public async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, CancellationToken cancellationToken = default)
        {
            var context = GetStoreContext();
            var results = await context.GetAppLicenseAsync();
            return (from item in results.AddOnLicenses
                    select item.Value.ToInAppBillingPurchase()).ToList();
        }
        public async Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId = null, string obfuscatedProfileId = null, string subOfferToken = null, CancellationToken cancellationToken = default)
        {
            var context = GetStoreContext();

            if(itemType == ItemType.InAppPurchase || itemType == ItemType.InAppPurchaseConsumable)
            {
                var result = await context.RequestPurchaseAsync(productId);
                switch (result.Status)
                {
                    case StorePurchaseStatus.AlreadyPurchased:
                        throw new InAppBillingPurchaseException(PurchaseError.AlreadyOwned, result.ExtendedError?.Message);
                    case StorePurchaseStatus.Succeeded:
                        {
                            return new InAppBillingPurchase
                            {
                                ProductId = productId,
                                State = PurchaseState.Purchased
                            };
                        }
                    case StorePurchaseStatus.NotPurchased:
                        throw new InAppBillingPurchaseException(PurchaseError.UserCancelled, result.ExtendedError?.Message);
                    case StorePurchaseStatus.NetworkError:
                        throw new InAppBillingPurchaseException(PurchaseError.ProductRequestFailed, result.ExtendedError?.Message);
                    case StorePurchaseStatus.ServerError:
                        throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, result.ExtendedError?.Message);
                    default:
                        throw new InAppBillingPurchaseException(PurchaseError.GeneralError, result.ExtendedError?.Message);
                }
            }
            else
            {
                var userOwnsSubscription = await CheckIfUserHasSubscriptionAsync(context, productId);
                if (userOwnsSubscription)
                {
                    // Unlock all the subscription add-on features here.
                    return null;
                }
                
                // Get the StoreProduct that represents the subscription add-on.
                var subscriptionStoreProduct = await GetSubscriptionProductAsync(context, productId);
                if (subscriptionStoreProduct == null)
                {
                    return null;
                }
                
                // Prompt the customer to purchase the subscription.
                // Request a purchase of the subscription product. If a trial is available it will be offered 
                // to the customer. Otherwise, the non-trial SKU will be offered.
                var result = await subscriptionStoreProduct.RequestPurchaseAsync();

                // Capture the error message for the operation, if any.
                var extendedError = string.Empty;
                if (result.ExtendedError != null)
                {
                    extendedError = result.ExtendedError.Message;
                }

                switch (result.Status)
                {
                    case StorePurchaseStatus.AlreadyPurchased:
                        throw new InAppBillingPurchaseException(PurchaseError.AlreadyOwned, result.ExtendedError?.Message);
                    case StorePurchaseStatus.Succeeded:
                        {
                            return new InAppBillingPurchase
                            {
                                ProductId = productId,
                                State = PurchaseState.Purchased
                            };
                        }
                    case StorePurchaseStatus.NotPurchased:
                        throw new InAppBillingPurchaseException(PurchaseError.UserCancelled, result.ExtendedError?.Message);
                    case StorePurchaseStatus.NetworkError:
                        throw new InAppBillingPurchaseException(PurchaseError.ProductRequestFailed, result.ExtendedError?.Message);
                    case StorePurchaseStatus.ServerError:
                        throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, result.ExtendedError?.Message);
                    default:
                        throw new InAppBillingPurchaseException(PurchaseError.GeneralError, result.ExtendedError?.Message);
                }
            }
          
        }
        public Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        private async Task<bool> CheckIfUserHasSubscriptionAsync(StoreContext context, string subscriptionStoreId)
        {
            var appLicense = await context.GetAppLicenseAsync();

            // Check if the customer has the rights to the subscription.
            foreach (var addOnLicense in appLicense.AddOnLicenses)
            {
                var license = addOnLicense.Value;
                if (license.SkuStoreId.StartsWith(subscriptionStoreId))
                {
                    if (license.IsActive)
                    {
                        // The expiration date is available in the license.ExpirationDate property.
                        return true;
                    }
                }
            }

            // The customer does not have a license to the subscription.
            return false;
        }

        private async Task<StoreProduct> GetSubscriptionProductAsync(StoreContext context, string subscriptionStoreId)
        {
            // Load the sellable add-ons for this app and check if the trial is still 
            // available for this customer. If they previously acquired a trial they won't 
            // be able to get a trial again, and the StoreProduct.Skus property will 
            // only contain one SKU.
            var result =
                await context.GetAssociatedStoreProductsAsync(new string[] { "Durable" });

            if (result.ExtendedError != null)
            {
                System.Diagnostics.Debug.WriteLine("Something went wrong while getting the add-ons. " +
                    "ExtendedError:" + result.ExtendedError);
                return null;
            }

            // Look for the product that represents the subscription.
            foreach (var item in result.Products)
            {
                var product = item.Value;
                if (product.StoreId == subscriptionStoreId)
                {
                    return product;
                }
            }

            System.Diagnostics.Debug.WriteLine("The subscription was not found.");
            return null;
        }
    }    

    
    static class WindowsUtils
    {
        public static List<string> ToProductFilter(this ItemType itemType)
        {
            var filterList = new List<string>();
            if (itemType == ItemType.InAppPurchase)
                filterList.Add("Durable");
            else if (itemType == ItemType.InAppPurchaseConsumable)
            {
                filterList.Add("Consumable");
                filterList.Add("UnmanagedConsumable");
            }

            return filterList;
        }


        public static InAppBillingPurchase ToInAppBillingPurchase(this StoreLicense license)
        {
            return new InAppBillingPurchase
            {
                ProductId = license.InAppOfferToken,
                State = license.IsActive ? PurchaseState.Purchased : PurchaseState.Unknown,
                ExpirationDate = license.ExpirationDate,
                OriginalJson = license.ExtendedJsonData
            };
        }

        public static InAppBillingProduct ToInAppBillingProduct(this StoreProduct product)
        {
            return new InAppBillingProduct
            {
                Name = product.Title,
                Description = product.Description,
                ProductId = product.StoreId,
                LocalizedPrice = product.Price.FormattedPrice,
                WindowsExtras = new InAppBillingProductWindowsExtras
                {
                    ExtendedJsonData = product.ExtendedJsonData,
                    HasDigitalDownload = product.HasDigitalDownload,
                    InAppOfferToken = product.InAppOfferToken,
                    IsInUserCollection = product.IsInUserCollection,
                    Language = product.Language,
                    LinkUri = product.LinkUri,
                    FormattedBasePrice = product.Price.FormattedBasePrice,
                    FormattedRecurrencePrice = product.Price.FormattedRecurrencePrice,
                    IsOnSale = product.Price.IsOnSale,
                    SaleEndDate = product.Price.SaleEndDate,
                    IsConsumable = product.ProductKind == "Consumable",
                    IsDurable = product.ProductKind == "Durable",
                    IsUnmanagedConsumable = product.ProductKind == "UnmanagedConsumable",
                    Keywords = product.Keywords
                },
                CurrencyCode = product.Price.CurrencyCode // Does not work at the moment, as UWP throws an InvalidCastException when getting CurrencyCode
            };
        }
    }

}