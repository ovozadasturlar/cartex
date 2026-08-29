using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Cartex.Mobile.Store.Services;

public sealed class ProductChangedMessage(long variantId) : ValueChangedMessage<long>(variantId);
