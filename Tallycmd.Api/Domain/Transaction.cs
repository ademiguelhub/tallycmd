namespace Tallycmd.Api.Domain
{
    public class Transaction
    {
        public int Id { get; set; }
        public required string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}