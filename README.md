# Overview
### SaleManager is a desktop application designed to manage orders and assist during sales by providing information about the available assortment and aiding in the calculation of products needed.

# Features
- Users can add products to the list of available items.
- Unlimited creation of sales is supported.
- Products can be linked to two user-selected keys for efficient insertion.
- Clients are shared across different sales.
- Client filtering is available based on ordered or bought products, name, phone number, tags and ordered amount/value.
- Clients can have notes and tags (for example `po terminie`) that can be used to filter the list.
- A client history screen (`Historia`) shows a client's sales, money spent, orders not collected and favourite products.
- The client screen saves itself every 20 seconds; closing the program saves first.
- Statistics: order accuracy, revenue and trends per sale, and product performance with a suggested quantity for the next sale.
- Automatic local backups of the database (at program start, last 20 kept) with a restore screen (`Ustawienia` -> `Kopie zapasowe`).
- Dark and light theme and three text sizes (`Ustawienia`).

# Keyboard shortcuts
- Client list: `Ctrl+F` search, `Enter` in the search opens the first client, `Ctrl+N` new client, `Esc` clears the search and then goes back.
- Client screen: `Ctrl+S` save, `Esc` save and go back.

# Usage Guide 
![image](https://github.com/RafalZmu/SaleManager/assets/36961066/5c44e398-27e1-46ff-a4fc-b8df1c6f11dc)


### To add new products, navigate to the "Produkty" tab.
### Clicking "Dodaj Produkty" adds a new empty product to the center area.
### The leftmost column represents the code used for quick product insertion. The middle column displays the product name, while the rightmost column shows the price per kilogram.
![image-1](https://github.com/RafalZmu/SaleManager/assets/36961066/e692d018-4e43-438a-b01a-6adc8d09aaeb)

## Creating new sale
### Return to the previous screen, input the chosen sale name into the text field and press the + button.
![image-2](https://github.com/RafalZmu/SaleManager/assets/36961066/81c5207f-0bfa-4f43-86fc-8c4ccb7653ab)

### The selected sale screen displays all clients from all sales. Clients with green text have bought products in this sale, those with red text have ordered but not bought products, and those with white text have neither ordered nor bought products.

## Filtering Clients
### Clicking the "Filtr" button opens a popup menu showing all added products. Checkboxes can be used to apply filters to all clients. 

![image-4](https://github.com/RafalZmu/SaleManager/assets/36961066/d7975e50-a11d-446a-b6eb-4ff94401e56a)

## Adding Clients to Sale

### Press the "Dodaj Klienta" button to add new clients to the sale.

### The client edition screen features four textboxes:
- The top two textboxes store the name/nickname and phone number.
- The bottom two large text fields store ordered products (left) and bought products (right).

### A list of all products and their codes is on the left. The bottom of the bought items field displays the total cost of all bought products and any possible change

### To insert a product, type its code, which will automatically be transformed into the correct format.

![image-5](https://github.com/RafalZmu/SaleManager/assets/36961066/6b6413ff-1375-4e51-9c17-025c5ea3ca78)

## Sale Summary

### While in the sale, pressing the "Podsumowanie" button will take you to the sale summary screen. This screen displays three rows in order: Still left orders, All orders (including collected ones), and the weight of already sold products. The top number shows how many clients have not yet collected their orders.
![image-6](https://github.com/RafalZmu/SaleManager/assets/36961066/15ce07b8-76bd-450f-baba-b95f44989be3)


# Known limitations
- When adding products to client make sure to follow the structure |Product: Weight|
- Comment are supperted but can't be inserted between the product name and the weight


