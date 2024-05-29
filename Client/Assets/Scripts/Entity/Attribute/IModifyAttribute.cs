namespace Entity.Attribute
{
    public interface IModifyAttribute
    {
        public object GetFinalValue();
        public void AddBaseNum(object addValue);
        public void SetBaseNum(object baseNum);
        public void AddNum(object addValue);
        public void AddPercent(int addPercent);
    }
}