using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{
    [Table("Doctors")]
    public class Doctor
    {
        [Column("Id")]
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        [Column("Specialty")]
        public string Specialty { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [StringLength(255)]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;
    }
}