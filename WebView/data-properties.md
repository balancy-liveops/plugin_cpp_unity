# Properties for WebView


## General Info

* 'data-localization-key' - This property is used to specify the localization key for the WebView text. It allows the content to be localized based on the user's language preferences.
* 'data-image-id' - This property is used to specify the ID of the image that should be displayed in the WebView. It allows using cached images.
* 'data-button-action' - predefined actions for buttons in the WebView. It can be used to trigger specific actions when a button is clicked.
* 'data-index' - used for RequestAction::BuyGroupOffer - to define the index of the StoreItem in the group offer list (starts from 0).
* 'data-custom' - any custom string

## Pricing

We can define the price for the text on buttons:

* 'data-text-auto' - used to launch auto-text replacement. RequestAction::GetInfo is invoked

* 'data-info-type' - defines what kind of info is expected to be received (int).
    
    const InfoType = {
        None: 0,
        OfferPrice: 1,
        OfferGroupPrice: 2,
        CustomPrice: 9,
        Custom: 10
    }

* 'data-index' - used for the InfoType::OfferGroupPrice group to define the index of the StoreItem in the group offer list (starts from 0).
* 'data-product-id' - used for the case InfoType::CustomPrice
* 'data-text-format' - function, like **(info) => `${info.LocalizedPriceString}`**, that's called when the priceInfo is received
