using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace AppDomain
{
    public class ComPortPair
    {
        public string Name { get; set; }

        [Required(ErrorMessage = "Sender is required")]
        public string Sender { get; set; }

        [Required(ErrorMessage = "Sender is required")]
        public string Receiver { get; set; }
    }
}
