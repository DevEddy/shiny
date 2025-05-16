using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Foundation;

namespace Shiny.Push;


public class FirebasePushProvider(FirebaseConfiguration config) : NotifyPropertyChanged, IPushProvider, IPushTagSupport
{

    public async Task<string> Register(NSData nativeToken)
    {
        this.TryStartFirebase();
        Firebase.CloudMessaging.Messaging.SharedInstance.ApnsToken = nativeToken;
        var fcmToken = await Firebase.CloudMessaging.Messaging.SharedInstance.FetchTokenAsync();
        if (fcmToken == null)
             throw new InvalidOperationException("FCM Token is null");
        
        return fcmToken;
    }


    public Task UnRegister()
    {
        this.RegisteredTags = null;
        return Firebase.CloudMessaging.Messaging.SharedInstance.DeleteTokenAsync();
    }


    string[]? registeredTags;
    public string[]? RegisteredTags
    {
        get => this.registeredTags;
        set => this.Set(ref this.registeredTags, value);
    }


    public async Task AddTag(string tag)
    {
        var tags = this.RegisteredTags?.ToList() ?? new List<string>(1);
        tags.Add(tag);

        await Firebase.CloudMessaging.Messaging.SharedInstance.SubscribeAsync(tag).ConfigureAwait(false);
        this.RegisteredTags = tags.ToArray();
    }


    public async Task RemoveTag(string tag)
    {
        await Firebase.CloudMessaging.Messaging.SharedInstance
            .UnsubscribeAsync(tag)
            .ConfigureAwait(false);

        if (this.RegisteredTags != null)
        {
            var tags = this.RegisteredTags.ToList();
            if (tags.Remove(tag))
                this.RegisteredTags = tags.ToArray();
        }
    }


    public async Task ClearTags()
    {
        if (this.RegisteredTags != null)
        {
            foreach (var tag in this.RegisteredTags)
            {
                await Firebase.CloudMessaging.Messaging.SharedInstance
                    .UnsubscribeAsync(tag)
                    .ConfigureAwait(false);
            }
        }
        this.RegisteredTags = null;
    }


    public async Task SetTags(params string[]? tags)
    {
        await this.ClearTags().ConfigureAwait(false);
        if (tags != null)
        {
            foreach (var tag in tags)
                await this.AddTag(tag).ConfigureAwait(false);
        }
    }


    protected virtual void TryStartFirebase()
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        var isAlreadyConfigured = Firebase.Core.App.DefaultInstance != null;
        if (isAlreadyConfigured) 
            return;
        
        if (config.UseEmbeddedConfiguration)
        {
            Firebase.Core.App.Configure();
            Firebase.CloudMessaging.Messaging.SharedInstance.AutoInitEnabled = true;
        }
        else
        {
            var options = new Firebase.Core.Options(googleAppId: config.AppId!, gcmSenderId: config.SenderId!);
            options.ApiKey = config.ApiKey;
            options.ProjectId = config.ProjectId;
            Firebase.Core.App.Configure(options);
        }
        
        var wasConfiguredSuccessfully = Firebase.Core.App.DefaultInstance != null;
        if (!wasConfiguredSuccessfully)
            throw new InvalidOperationException("Firebase Application failed to configure - please check your settings");
    }
}