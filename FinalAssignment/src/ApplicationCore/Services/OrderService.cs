using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly HttpClient _httpClient;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
         HttpClient httpClient)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _httpClient = httpClient;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);
        
        // Calling to Azure function - http request triggered
        await SaveOrderItem(order);
        //Calling to Azure function -service bus triggered
        await ReserveOrderItem(new ReserveOrder(basketId, items.Count));

        await _orderRepository.AddAsync(order);
    }

    /// <summary>
    /// Order Items Reserver function should be able to create Reservation 
    /// JSON files in Azure Blob Storage by communicating through Service Bus
    /// </summary>
    /// <param name="reserveOrder"></param>
    /// <returns></returns>
    private async Task<string> ReserveOrderItem(ReserveOrder reserveOrder)
    {
        string connectionString = "Endpoint=sb://esservicebus666.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=zpO5RPzlnwTG0DqDqcEGZL/ZNDtrteyJPkO5MwWEVk0=";
        string QueueName = "saveorderqueue";
        // create a ServiceBusClient object using the connection string to the namespace
        await using var client = new ServiceBusClient(connectionString);
        await using ServiceBusSender sender = client.CreateSender(QueueName);
        try
        {

            var serializedMessage = JsonSerializer.Serialize(reserveOrder);
            ServiceBusMessage message = new ServiceBusMessage(serializedMessage);
            await sender.SendMessageAsync(message);
        }
        catch (Exception exception)
        {

        }
        finally
        {
            // Calling DisposeAsync on client types is required to ensure that network
            // resources and other unmanaged objects are properly cleaned up.
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }

        return string.Empty;
    }

    /// <summary>
    /// Delivery Order Processor functions to be triggered after the order 
    /// has been created to store details in cosmos DB
    /// </summary>
    /// <param name="saveOrder"></param>
    /// <returns></returns>
    private async Task<string> SaveOrderItem(Order saveOrder)
    {
        var content = new StringContent(JsonSerializer.Serialize(saveOrder), Encoding.UTF8, "application/json");
       // var result = await _httpClient.PostAsync("http://localhost:7071/api/DeliveryOrderProcessor", content);
        var result = await _httpClient.PostAsync("https://deliveryorderprocessor666.azurewebsites.net", content);
        return result.Content.ReadAsStringAsync().Result;
    }
}

public class ReserveOrder
{
    int itemId;
    int quantity;

    public int ItemId { get => itemId; set => itemId = value; }
    public int Quantity { get => quantity; set => quantity = value; }

    public ReserveOrder(int itemId, int quantity)
    {
        this.itemId = itemId;
        this.quantity = quantity;
    }
}
