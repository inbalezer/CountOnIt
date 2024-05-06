using CountOnIt.Shared.Models.present.toAdd;
using CountOnIt.Shared.Models.present.toEdit;
using CountOnIt.Shared.Models.present.toShow;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TriangleDbRepository;

namespace CountOnIt.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly DbRepository _db;

        public TransactionsController(DbRepository db)
        {
            _db = db;
        }

        [HttpPost("AddTransaction")] // יצירת הזנה חדשה
        public async Task<IActionResult> AddTransaction(TransactionToAdd TransactionToAdd)
        {
            object TransToAddParam = new
            {
                transTitle = TransactionToAdd.transTitle,
                subCategoryID = TransactionToAdd.subCategoryID,
                transType = TransactionToAdd.transType,
                transValue = TransactionToAdd.transValue,
                valueType = TransactionToAdd.valueType,
                transDate = TransactionToAdd.transDate,
                description = TransactionToAdd.description,
                fixedMonthly = TransactionToAdd.fixedMonthly,
                parentTransID = TransactionToAdd.parentTransID,
                tagID = TransactionToAdd.tagID
            };

            string insertTransQuery = "INSERT INTO transactions (transTitle,subCategoryID, transType, transValue, valueType, transDate, description, fixedMonthly, parentTransID, tagID) values (@transTitle,@subCategoryID, @transType, @transValue, @valueType, @transDate, @description, @fixedMonthly, @parentTransID, @tagID)";

            int newTransId = await _db.InsertReturnId(insertTransQuery, TransToAddParam);

            if (newTransId != 0)
            {
                return Ok(newTransId);
            }

            return BadRequest("Transaction not created");
        }

        [HttpGet("showOverdraft/{subCatID}/{userID}")]
        public async Task<IActionResult> showOverdraft(int subCatID, int userID)
        {
            List<OverBudgetToShow> subcategoriesToCloseGap = new List<OverBudgetToShow>();
            List<OverBudgetToShow> subcategoriesToCloseGapAfterLoop = new List<OverBudgetToShow>();
            OverBudgetToShow currentOverdraft = new OverBudgetToShow();

            object param = new
            {
                ID = subCatID
            };

            string GetCategoryCurrentSumQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID";
            var recordSubCatCurrentSum = await _db.GetRecordsAsync<double>(GetCategoryCurrentSumQuery, param);
            if (recordSubCatCurrentSum != null)
            {
                string GetSubCategoryBudgetQuery = "SELECT monthlyPlannedBudget FROM subcategories WHERE id = @ID";
                var budgetRes = await _db.GetRecordsAsync<int>(GetSubCategoryBudgetQuery, param);
                if (budgetRes != null)
                {
                    string GetSubCategoryNameQuery = "SELECT subCategoryTitle FROM subcategories WHERE id = @ID";
                    var recordGetSubCategoryName = await _db.GetRecordsAsync<string>(GetSubCategoryNameQuery, param);
                    if (recordGetSubCategoryName != null)
                    {
                        //currentOverdraft is different than the other list items, its remaining budget is the sum of all its expenses, when in the other list items it is the budget free to close the gap
                        currentOverdraft.subCategoryTitle = recordGetSubCategoryName.FirstOrDefault();
                        currentOverdraft.monthlyPlannedBudget = budgetRes.FirstOrDefault();
                        currentOverdraft.remainingBudget = recordSubCatCurrentSum.FirstOrDefault();
                        currentOverdraft.id = subCatID;

                        if(currentOverdraft.monthlyPlannedBudget >= currentOverdraft.remainingBudget)
                        {
                            return BadRequest("אין חריגה");
                        }

                        double gap = (currentOverdraft.remainingBudget - currentOverdraft.monthlyPlannedBudget);
                        Console.WriteLine("the budget gap is- " + gap);


                        object userCatParam = new
                        {
                            userID = userID
                        };

                        string getUserCategories = "SELECT id FROM categories where userID = @userID";
                        var recordUserCategories = await _db.GetRecordsAsync<int>(getUserCategories, userCatParam);
                        List<int> userCatesList = recordUserCategories.ToList();

                        if (userCatesList.Count > 0)
                        {
                            foreach (var category in userCatesList)
                            {
                                if (category != null && category > 0)
                                {

                                    object gapParam = new
                                    {
                                        gap = gap,
                                        categoryID = category
                                    };

                                    string getFittingSubCats = "SELECT subcategories.id, subcategories.subCategoryTitle, subcategories.monthlyPlannedBudget, (monthlyPlannedBudget - COALESCE(SUM(transValue), 0)) AS remainingBudget FROM subcategories LEFT JOIN transactions ON subcategories.id = transactions.subCategoryID WHERE importance = 0 AND categoryID = @categoryID GROUP BY subcategories.id HAVING remainingBudget > @gap";

                                    var optionalSubcategories = await _db.GetRecordsAsync<OverBudgetToShow>(getFittingSubCats, gapParam);

                                    if (optionalSubcategories != null )
                                    {
                                        subcategoriesToCloseGap = optionalSubcategories.ToList();     
                                        
                                        foreach(OverBudgetToShow subCatToShow in subcategoriesToCloseGap)
                                        {
                                            subcategoriesToCloseGapAfterLoop.Add(subCatToShow);
                                        }
                                    }                                  
                                }                                
                            }
                            subcategoriesToCloseGapAfterLoop.Add(currentOverdraft);
                            return Ok(subcategoriesToCloseGapAfterLoop);
                        }
                        return BadRequest("no category found");
                    }
                    return BadRequest("no sub category found");
                }
                return BadRequest("no linked transactions found to this subcategory");
            }
            return BadRequest("couldnt find sum");
        }

        [HttpPost("EditSubCategoriesNewBudgets")]  // עריכת תקציב חדש לאחר העברה בחריגה

        public async Task<IActionResult> EditSubCategoriesNewBudgets([FromBody] List<OverDraftBudgetToEdit> budgetToUpdate)
        {

            bool isBudgetUpdate = false;

            foreach (var newBudget in budgetToUpdate)
            {

                object updateBudgetParam = new
                {
                    ID = newBudget.id,
                    monthlyPlannedBudget = newBudget.monthlyPlannedBudget
                };

                string UpdateSubCategoryBudgetQuery = "UPDATE subcategories set monthlyPlannedBudget = @monthlyPlannedBudget where id =@ID";
                isBudgetUpdate = await _db.SaveDataAsync(UpdateSubCategoryBudgetQuery, updateBudgetParam);

            }

            if (isBudgetUpdate)
            {
                return Ok("התקציב עודכן בהצלחה");
            }
            return BadRequest("update sub category budget failed");


        }

        [HttpGet("updateGivingSubCat/{subCatID}")]
        public async Task<IActionResult> updateGivingSubCat(int subCatID)
        {
            object param = new
            {
                ID = subCatID
            };

            string GetSubCatCurrentBudgetQuery = "SELECT monthlyPlannedBudget FROM subcategories WHERE id = @ID";
            var recordSubCatCurrentSum = await _db.GetRecordsAsync<double>(GetSubCatCurrentBudgetQuery, param);
            double newBudget= recordSubCatCurrentSum.FirstOrDefault();
            if (newBudget!=null)
            {
                return Ok(newBudget);
            }

            return BadRequest("Couldn't find this sub cat's budget");
        }

    }
}
