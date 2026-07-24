SELECT p.Id, p.Name, COUNT(oi.Id) AS OrderItemCount
FROM Products p
LEFT JOIN OrderItems oi ON oi.ProductId = p.Id
WHERE p.Description = 'Synced from Gateway'
GROUP BY p.Id, p.Name;
