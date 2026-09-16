using System;
using System.Collections.Generic;
using System.Text;

namespace FCanteen.Data.Entities
{
    public class Ingredient
    {
        public int IngredientId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        public decimal StockQuantity { get; set; }

        public decimal AlertThreshold { get; set; }
    }
}
