
namespace Cartex.Mobile.Core;

[ContentProperty(nameof(Key))]
public sealed class TExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = "";

    public BindingBase ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]", BindingMode.OneWay, source: Loc.Instance);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
