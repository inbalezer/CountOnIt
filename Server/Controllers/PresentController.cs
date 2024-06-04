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
    public class PresentController : ControllerBase
    {
        private readonly DbRepository _db;

        public PresentController(DbRepository db)
        {
            _db = db;
        }



        [HttpGet("userToShow/{userGoogleID}")] // שליפה של התצוגה הראשונית בסביבה השניה לפני לחיצות על כפתורים
        public async Task<IActionResult> GetUser(string userGoogleID)
        {
            // Initialize the SQL queries
            var userQuery = "SELECT id, firstName, profilePicOrIcon FROM users WHERE googleID = @ID";
            var categoryQuery = "SELECT id, categroyTitle, icon, color FROM categories WHERE userID = @ID";
            var subCategoryBudgetQuery = "SELECT COALESCE(SUM(monthlyPlannedBudget), 0) FROM subcategories WHERE categoryID = @ID";
            var transactionSumQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID AND transType = @TransType";

            // Get user details
            var user = (await _db.GetRecordsAsync<userToShow>(userQuery, new { ID = userGoogleID })).FirstOrDefault();
            if (user == null)
            {
                return BadRequest("User not found");
            }
            else
            {
                // Get categories for the user
                var categories = (await _db.GetRecordsAsync<CategoryToShow>(categoryQuery, new { ID = user.id })).ToList();
                if (categories.Any())
                {
                    user.categoriesFullList = categories;

                    double totalBudget = 0;
                    double totalSpendings = 0;
                    double totalIncome = 0;
                    foreach (var category in categories)
                    {
                        // Get total budget for each category
                        double categoryBudget = (await _db.GetRecordsAsync<double>(subCategoryBudgetQuery, new { ID = category.id })).FirstOrDefault();
                        totalBudget += categoryBudget;

                        //if (category.id == categories.First().id) // Assuming you want to calculate transactions for the first category only.
                        //{
                            // Calculate transaction sums for the first category
                            user.spendingValueFullList = (await _db.GetRecordsAsync<double>(transactionSumQuery, new { ID = category.id, TransType = 1 })).FirstOrDefault();
                        totalSpendings += user.spendingValueFullList;
                            user.incomeValueFullList = (await _db.GetRecordsAsync<double>(transactionSumQuery, new { ID = category.id, TransType = 2 })).FirstOrDefault();
                        totalIncome+= user.incomeValueFullList;
                        //}
                    }

                    // Calculate the budget usage percentage
                    user.budgetFullValue = CalculateBudgetPercentage(totalBudget, totalSpendings);
                    user.spendingValueFullList = totalSpendings;
                    user.incomeValueFullList= totalIncome;
                }

                return Ok(user);
            }

        }

        private double CalculateBudgetPercentage(double budget, double spending)
        {
            if (spending == 0)
                return 0;
            return Math.Round((spending / budget) * 100, 2);
        }

        [HttpGet("incomeCatId/{userID}")] //gets the ID of the income category
        public async Task<IActionResult> GetUserIncomeID(int userID)
        {
            object param = new
            {
                ID = userID
            };

            var userIncomeQuery = "SELECT c.id AS incomeID FROM categories as c, users as u where c.userID=u.id and c.categroyTitle=\"הכנסות\" and u.id=@ID";

            var getIncomeID= await _db.GetRecordsAsync<int>(userIncomeQuery, param);
            int incomeCatID = getIncomeID.FirstOrDefault();
            if (incomeCatID>0)
            {
                return Ok(incomeCatID);
            }
            return BadRequest("couldn't find income category that belongs to this user");
        }

        [HttpGet("subCategoryToShow/{categoryID}")] // הצגה של תתי קטגוריות בלחיצה על הדרופדאון
        public async Task<IActionResult> subCategoryToShow(int categoryID)
        {
            object param = new
            {
                ID = categoryID
            };

            // צריך להוסיף שאילתה שמושכת את הצבע מהקטגוריה בשביל להציג גם בתתי קטגוריות אם בחר צבע.

            string GetSubCategoriesQuery = "SELECT id, subCategoryTitle, monthlyPlannedBudget FROM subcategories WHERE categoryID = @ID";
            var recordsubCategories = await _db.GetRecordsAsync<SubCategoryToShow>(GetSubCategoriesQuery, param);
            List<SubCategoryToShow> subCategories = recordsubCategories.ToList();

            if (subCategories.Count > 0)
            {
                foreach (var subCategory in subCategories)
                {
                    if (subCategory.id != null)
                    {
                        object subParam = new
                        {
                            ID = subCategory.id
                        };

                        string GetTransactionValueQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID AND (transType = 1 OR transType = 3) and MONTH(transDate) = MONTH(CURRENT_DATE()) AND YEAR(transDate) = YEAR(CURRENT_DATE()); ";

                        var recordsTransValue = await _db.GetRecordsAsync<double>(GetTransactionValueQuery, subParam);
                        subCategory.transactionsValue = recordsTransValue.FirstOrDefault();
                    }
                    else
                    {
                        return BadRequest("no transaction in sub category");
                    }

                }

                return Ok(subCategories);
            }

            return BadRequest("user not found");
        }

        [HttpGet("getIncomeSubCats/{categoryID}")]
        public async Task<IActionResult> incomeSubCategoryToShow(int categoryID)
        {
            object param = new
            {
                ID = categoryID
            };

            // צריך להוסיף שאילתה שמושכת את הצבע מהקטגוריה בשביל להציג גם בתתי קטגוריות אם בחר צבע.

            string GetSubCategoriesQuery = "SELECT id, subCategoryTitle, monthlyPlannedBudget FROM subcategories WHERE categoryID = @ID";
            var recordsubCategories = await _db.GetRecordsAsync<SubCategoryToShow>(GetSubCategoriesQuery, param);
            List<SubCategoryToShow> subCategories = recordsubCategories.ToList();

            if (subCategories.Count > 0)
            {
                foreach (var subCategory in subCategories)
                {
                    if (subCategory.id != null)
                    {
                        object subParam = new
                        {
                            ID = subCategory.id
                        };

                        string GetTransactionValueQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID AND transType = 2 and MONTH(transDate) = MONTH(CURRENT_DATE()) AND YEAR(transDate) = YEAR(CURRENT_DATE());";

                        var recordsTransValue = await _db.GetRecordsAsync<double>(GetTransactionValueQuery, subParam);
                        subCategory.transactionsValue = recordsTransValue.FirstOrDefault();
                    }
                    else
                    {
                        return BadRequest("no transaction in sub category");
                    }

                }

                return Ok(subCategories);
            }

            return BadRequest("no sub categories found");
        }

        [HttpGet("GetCategoryToEdit/{CategoryId}")] // שליפת קטגוריה לעריכה
        public async Task<IActionResult> GetFullCategory(int CategoryId)
        {

            object param = new
            {
                ID = CategoryId
            };

            string GetCategoryQuery = "SELECT id, categroyTitle, icon, color FROM categories WHERE id = @ID";

            var recordCategory = await _db.GetRecordsAsync<CategoryToShow>(GetCategoryQuery, param);
            CategoryToShow category = recordCategory.FirstOrDefault();

            if (category != null)
            {
                return Ok(category);
            }
            return BadRequest("category not found");
        }


        [HttpPost("EditCategory")]  // עריכת קטגוריה

        public async Task<IActionResult> editCategory(CategoryToEdit categoryToUpdate)
        {

            object updateParam = new
            {
                ID = categoryToUpdate.id,
                categroyTitle = categoryToUpdate.categroyTitle,
                icon = categoryToUpdate.icon,
                color = categoryToUpdate.color
            };

            string UpdateCategoryQuery = "UPDATE categories set categroyTitle = @categroyTitle, icon = @icon, color = @color where id =@ID";
            bool isUpdate = await _db.SaveDataAsync(UpdateCategoryQuery, updateParam);

            if (isUpdate)
            {
                return Ok(categoryToUpdate);
            }
            return BadRequest("update category faild");
        }



        [HttpPost("AddCategory/{userID}")] // יצירת קטגוריה חדשה
        public async Task<IActionResult> AddCategory(int userID, CategoryToAdd categoryToAdd) // לראות איך היוזר אידי מגיע מהפרונט אנד 
        {
            object categoryToAddParam = new
            {
                userID = userID,
                categroyTitle = categoryToAdd.categroyTitle,
                icon = categoryToAdd.icon,
                color = categoryToAdd.color
            };

            string insertCategoryQuery = "INSERT INTO categories (categroyTitle,userID, icon, color) values (@categroyTitle ,@userID ,@icon ,@color)";

            int newCategoryId = await _db.InsertReturnId(insertCategoryQuery, categoryToAddParam);

            if (newCategoryId != 0)
            {
                object param = new
                {
                    id = newCategoryId,
                    userID = categoryToAdd.userID
                };

                string GetCategoryQuery = "SELECT id, categroyTitle, icon, color FROM categories WHERE id = @id AND userID = @userID";
                var recordsCategory = await _db.GetRecordsAsync<CategoryToShow>(GetCategoryQuery, param);
                CategoryToShow category = recordsCategory.FirstOrDefault();

                if (category != null)
                {
                    return Ok(category);
                }
                return BadRequest("category not found");

            }

            return BadRequest("category not created");
        }

        [HttpDelete("deleteCategory/{CategoryIdToDelete}")] // מחיקת קטגוריה
        public async Task<IActionResult> DeleteCategory(int CategoryIdToDelete)
        {
            string DeleteQuery = "DELETE FROM categories WHERE id=@ID";
            bool isCategoryDeleted = await _db.SaveDataAsync(DeleteQuery, new { ID = CategoryIdToDelete });

            if (isCategoryDeleted)
            {
                return Ok();
            }

            return BadRequest("Failed to delete category");
        }


        [HttpGet("GetCategoriesOverview/{userID}")] // שליפת קטגוריות וסכומים לעמוד סטורי 3 במצב החודש כרגע
        public async Task<IActionResult> GetCategoriesOverview(int userID)
        {
            object param = new { ID = userID };

            string GetCategoryOverviewIDQuery = "SELECT id FROM categories WHERE userID = @ID";
            var recordCategoryOverviewID = await _db.GetRecordsAsync<int>(GetCategoryOverviewIDQuery, param);

            if (recordCategoryOverviewID == null)
            {
                return Ok(new List<CategoriesOverviewToShow>());  // Return an empty list if no categories found
            }

            List<CategoriesOverviewToShow> categoriesOverviewToShowList = new List<CategoriesOverviewToShow>();

            foreach (int catID in recordCategoryOverviewID)
            {
                double subCatSum = 0;  // Reset sum for each category

                object categoryParam = new { ID = catID };


                string GetSubCategoryIDQuery = "SELECT id FROM subcategories WHERE categoryID = @ID";
                var recordSubCategoryID = await _db.GetRecordsAsync<int>(GetSubCategoryIDQuery, categoryParam);

                if (recordSubCategoryID != null)
                {
                    foreach (int subCatID in recordSubCategoryID)
                    {
                        object subCatIDParam = new { ID = subCatID };

                        // Expenses:
                        string GetCategoryCurrentSumQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID";
                        var recordSubCatCurrentSum = await _db.GetRecordsAsync<double>(GetCategoryCurrentSumQuery, subCatIDParam);
                        subCatSum += recordSubCatCurrentSum.FirstOrDefault();

                        //    // Income:
                        //    string GetIncomesQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID AND transType = 2";
                        //    var recordSubCatCurrentSumIncome = await _db.GetRecordsAsync<double>(GetIncomesQuery, subCatIDParam);
                        //    subCatSum += recordSubCatCurrentSumIncome.FirstOrDefault();
                        //}
                    }

                    string GetCategoryTitleOverviewQuery = "SELECT categroyTitle FROM categories WHERE id = @ID"; // Correct potential typo from 'categroyTitle' to 'categoryTitle'
                    var getCategoryTitle = await _db.GetRecordsAsync<string>(GetCategoryTitleOverviewQuery, categoryParam);
                    CategoriesOverviewToShow currentCategoryStats = new CategoriesOverviewToShow();

                    if (getCategoryTitle != null)
                    {
                        currentCategoryStats.categroyTitle = getCategoryTitle.FirstOrDefault(); // Also corrected the property name if typo existed
                        currentCategoryStats.currentCategorySum = subCatSum;
                    }

                    categoriesOverviewToShowList.Add(currentCategoryStats);
                }
            }
            return Ok(categoriesOverviewToShowList);
        }


        [HttpGet("GetTagsAndSpendings/{userID}")] // שליפת תגיות וסכומים שלהן לעמוד סטורי 2 במצב החודש כרגע
        public async Task<IActionResult> GetTagsAndSpendings(int userID)
        {
            List<TagsAndSpendingsToShow> TagsAndSpendingsToShowList = new List<TagsAndSpendingsToShow>();

            object param = new
            {
                ID = userID
            };

            string GetTagsAndSpendingsQuery = @"
        SELECT t.tagID, tg.tagTitle, tg.tagColor, SUM(t.transValue) as totalValue 
        FROM transactions t
        JOIN tags tg ON t.tagID = tg.id
        JOIN subcategories sc ON t.subCategoryID = sc.id
        JOIN categories c ON sc.categoryID = c.id
        WHERE c.userID = @ID AND t.transType = 1 AND MONTH(t.transDate) = MONTH(CURRENT_DATE())
    AND YEAR(t.transDate) = YEAR(CURRENT_DATE())
        GROUP BY t.tagID, tg.tagTitle, tg.tagColor 
        ORDER BY totalValue DESC 
        LIMIT 3";

            var recordTagsAndSpendings = await _db.GetRecordsAsync<TagsAndSpendingsToShow>(GetTagsAndSpendingsQuery, param);
            TagsAndSpendingsToShowList = recordTagsAndSpendings.ToList();

            if (TagsAndSpendingsToShowList != null && TagsAndSpendingsToShowList.Any())
            {
                return Ok(TagsAndSpendingsToShowList);
            }
            return BadRequest("Tags and Spendings not found");
        }

       
        [HttpGet("getBestAndWorstSpendings/{subCatID}")] //first content window of story
        public async Task<IActionResult> getBestAndWorstSpendings(int subCatID)
        {
            object subCatIDparam = new
            {
                ID = subCatID
            };

            string getSubTotalsQuery = "SELECT s.subCategoryTitle AS subCategoryTitle,COALESCE(SUM(CASE WHEN MONTH(t.transDate) = MONTH(CURRENT_DATE()) THEN t.transValue ELSE 0 END), 0) AS currentMonthTotal,COALESCE(SUM(CASE WHEN MONTH(t.transDate) = MONTH(DATE_SUB(CURRENT_DATE(), INTERVAL 1 MONTH)) THEN t.transValue ELSE 0 END), 0) AS lastMonthTotal FROM transactions t JOIN subcategories s ON t.subCategoryID = s.id where t.subCategoryID = s.id and s.id=@ID AND (t.transType = 1 OR t.transType = 3) GROUP BY s.subCategoryTitle;";

            var getTotalsRec = await _db.GetRecordsAsync<StorySubCategoryTotals>(getSubTotalsQuery, subCatIDparam);
            StorySubCategoryTotals requestedSubInfo = getTotalsRec.FirstOrDefault();
            if (requestedSubInfo!=null)
            {
                return Ok(requestedSubInfo);
            }

            return BadRequest("couldn't get this subcategory's monthly trans totals");
        }

        [HttpGet("GetSubCategoryToEdit/{SubCategoryId}")] // שליפת תת קטגוריה לעריכה
        public async Task<IActionResult> GetSubCategoryToEdit(int SubCategoryId)
        {
            object param = new
            {
                ID = SubCategoryId
            };

            // Updated SQL query to join the subcategories table with the category table
            string GetSubCategoryQuery = @"
        SELECT sc.id, sc.subCategoryTitle, sc.categoryID, sc.monthlyPlannedBudget, sc.importance, cat.categroyTitle 
        FROM subcategories sc
        JOIN categories cat ON sc.categoryID = cat.id
        WHERE sc.id = @ID";

            var recordSubCategory = await _db.GetRecordsAsync<SubCategoryToEdit>(GetSubCategoryQuery, param);
            SubCategoryToEdit subCategory = recordSubCategory.FirstOrDefault();

            if (subCategory != null)
            {
                return Ok(subCategory);
            }
            return BadRequest("Sub Category not found");
        }

        [HttpPost("EditSubCategory")]  // עריכת  תת קטגוריה

        public async Task<IActionResult> editSubCategory(SubCategoryToUpdate subCategoryToUpdate)
        {

            object subCategoryUpdateParam = new
            {
                ID = subCategoryToUpdate.id,
                subCategoryTitle = subCategoryToUpdate.subCategoryTitle,
                categoryID = subCategoryToUpdate.categoryID,
                monthlyPlannedBudget = subCategoryToUpdate.monthlyPlannedBudget,
                importance = subCategoryToUpdate.importance
            };

            string UpdateSubCategoryQuery = "UPDATE subcategories set subCategoryTitle = @subCategoryTitle, categoryID = @categoryID, monthlyPlannedBudget = @monthlyPlannedBudget, importance=@importance where id =@ID";
            bool isUpdate = await _db.SaveDataAsync(UpdateSubCategoryQuery, subCategoryUpdateParam);

            if (isUpdate)
            {
                return Ok(subCategoryToUpdate);
            }
            return BadRequest("update sub category failed");
        }

        [HttpPost("AddSubCategory")] // יצירת תת קטגוריה חדשה
        public async Task<IActionResult> AddSubCategory(SubCategoryToAdd subCategoryToAdd)
        {
            object subCategoryToAddParam = new
            {
                categoryID = subCategoryToAdd.categoryID,
                subCategoryTitle = subCategoryToAdd.subCategoryTitle,
                monthlyPlannedBudget = subCategoryToAdd.monthlyPlannedBudget ?? (int?)null,
                importance = subCategoryToAdd.importance ?? (int?)null
            };

            string insertSubCategoryQuery = "INSERT INTO subcategories (categoryID,subCategoryTitle,monthlyPlannedBudget, importance) values (@categoryID ,@subCategoryTitle ,@monthlyPlannedBudget ,@importance)";

            int newSubCategoryId = await _db.InsertReturnId(insertSubCategoryQuery, subCategoryToAddParam);

            if (newSubCategoryId != 0)
            {
                object param = new
                {
                    id = newSubCategoryId
                };

                string GetSubCategoryQuery = "SELECT id,categoryID,subCategoryTitle, monthlyPlannedBudget, importance FROM subcategories WHERE id = @id";
                var recordsSubCategory = await _db.GetRecordsAsync<SubCategoryToAdd>(GetSubCategoryQuery, param);
                SubCategoryToAdd subCategory = recordsSubCategory.FirstOrDefault();

                if (subCategory != null)
                {
                    return Ok(subCategory);
                }
                return BadRequest("sub category not found");

            }

            return BadRequest("sub category not created");
        }

        [HttpDelete("deleteSubCategory/{SubCategoryIdToDelete}")] // מחיקת תת קטגוריה
        public async Task<IActionResult> DeleteSubCategory(int SubCategoryIdToDelete)
        {
            string DeleteQuery = "DELETE FROM subcategories WHERE id=@ID";
            bool isSubCategoryDeleted = await _db.SaveDataAsync(DeleteQuery, new { ID = SubCategoryIdToDelete });

            if (isSubCategoryDeleted)
            {
                return Ok();
            }

            return BadRequest("Failed to delete sub category");
        }

        [HttpGet("GetUserCategories/{userID}")] // שליפת תת קטגוריה לעריכה
        public async Task<IActionResult> GetUserCategories(int userID)
        {

            object param = new
            {
                ID = userID
            };

            var usercategoriesQuery = "SELECT id, categroyTitle FROM categories WHERE userID = @ID AND categroyTitle != 'הכנסות';";
            var recordusercategoriesQuery = await _db.GetRecordsAsync<AllUserCategories>(usercategoriesQuery, param);
            List<AllUserCategories> allUserCategories = recordusercategoriesQuery.ToList();

            if (allUserCategories != null)
            {
                return Ok(allUserCategories);
            }
            return BadRequest("Category not found");
        }
        [HttpGet("GetIncomeCatID/{userID}")] // שליפת תת קטגוריה לעריכה
        public async Task<IActionResult> GetIncomeCatID(int userID)
        {

            object param = new
            {
                ID = userID
            };

            var usercategoriesQuery = "SELECT DISTINCT c.id, c.categroyTitle, c.icon, c.color FROM categories c JOIN subcategories sc ON c.id = sc.categoryID JOIN transactions t ON sc.id = t.subCategoryID WHERE t.transType = 2 and c.userID=@ID;";
            var recordusercategoriesQuery = await _db.GetRecordsAsync<CategoryToShow>(usercategoriesQuery, param);
            CategoryToShow allUserCategories = recordusercategoriesQuery.FirstOrDefault();

            if (allUserCategories != null)
            {
                return Ok(allUserCategories);
            }
            return BadRequest("Category not found");
        }

        [HttpPost("AddUser/{userGoogleID}")] // יצירת משתמש חזש
        public async Task<IActionResult> Adduser(string userGoogleID, UserToAdd userToAdd)
        {
            object userToAddParam = new
            {
                googleID = userGoogleID,
                firstName = userToAdd.firstName,
                lastName = userToAdd.lastName,
                profilePicOrIcon = userToAdd.profilePicOrIcon,
                monthStartDate = userToAdd.monthStartDate

            };

            string insertUserQuery = "INSERT INTO users (googleID,firstName,lastName,profilePicOrIcon,monthStartDate) values (@googleID ,@firstName ,@lastName, @profilePicOrIcon, @monthStartDate)";

            int newUserId = await _db.InsertReturnId(insertUserQuery, userToAddParam);

            if (newUserId != 0)
            {
                return Ok(newUserId);
            }

            return BadRequest("user not created");
        }

        [HttpGet("GetSubCatImportance/{subCatID}")] // שליפת עדיפות תת קטגוריה בעריכה
        public async Task<IActionResult> GetSubCatImportance(int subCatID)
        {

            object param = new
            {
                ID = subCatID
            };

            var subCatImportanceQuery = "SELECT importance FROM subcategories WHERE id=@ID";
            var importanceRec = await _db.GetRecordsAsync<int>(subCatImportanceQuery, param);
            int subCatImportance = importanceRec.FirstOrDefault();

            if (subCatImportance != null)
            {
                return Ok(subCatImportance);
            }
            return BadRequest("importance not found");
        }


        [HttpGet("getGivingCatID/{subCatID}")] //getting a giving sub-category's category ID
        public async Task<IActionResult> gettingCatID( int subCatID)
        {
            object param = new
            {
                ID = subCatID
            };

            var catIDQuery = "SELECT categoryID FROM subcategories where id=@ID";
            var catIDRec = await _db.GetRecordsAsync<int>(catIDQuery, param);
            int subCatParentID = catIDRec.FirstOrDefault();

            if (subCatParentID != null)
            {
                return Ok(subCatParentID);
            }
            return BadRequest("couldn't find this sub cat's category ID");
        }

        [HttpGet("getCategoryTitle/{catID}")]
        public async Task<IActionResult> getCategoryTitle(int catID)
        {
            object param = new
            {
                ID = catID
            };

            var catNameQuery = "SELECT categroyTitle FROM categories where id=@ID";
            var categoryRec = await _db.GetRecordsAsync<string>(catNameQuery, param);
            string categoryTitle = categoryRec.FirstOrDefault();

            if (categoryTitle != null)
            {
                return Ok( categoryTitle);
            }
            return BadRequest("couldn't find this category's title");
        }


        [HttpGet("getSubCategoryTitle/{userID}")]
        public async Task<ActionResult<string>> getSubCategoryTitle(int userID)
        {
            object param = new { ID = userID };
            var subcatIDQuery = "SELECT s.* FROM users u JOIN categories c ON u.Id = c.userID JOIN subcategories s ON c.Id = s.categoryID WHERE u.Id = @ID;";
            var subcatIDRec = await _db.GetRecordsAsync<SubCategoryToAdd>(subcatIDQuery, param);
            List<SubCategoryToAdd> subcategory = subcatIDRec.ToList();

            if (subcategory != null)
            {
                return Ok(subcategory);  // Return the subcategory directly
            }
            return BadRequest("Couldn't find this subcategory");
        }
        

        [HttpGet("getSubCategoriesForSearch")]
        public async Task<ActionResult<SubCategoryToAdd>> getSubCategoriesForSearch()
        {
            object param = new {};

            string GetSubCategoryForSearchQuery = "SELECT id,categoryID,subCategoryTitle, monthlyPlannedBudget, importance FROM subcategories WHERE categoryID = @id";
            var recordsSubCategory = await _db.GetRecordsAsync<SubCategoryToAdd>(GetSubCategoryForSearchQuery, param);
            SubCategoryToAdd subCategory = recordsSubCategory.FirstOrDefault();

            if (subCategory != null)
            {
                return Ok(subCategory);
            }
            return BadRequest("sub category not found");
        }

        [HttpGet("GetSubCategory/{SubCategoryId}")]
        public async Task<IActionResult> GetSubCategory(int SubCategoryId)
        {
            object param = new
            {
                ID = SubCategoryId
            };

            // Updated SQL query to join the subcategories table with the category table
            string GetSubCategoryQuery = @"
        SELECT id, subCategoryTitle, categoryID, monthlyPlannedBudget, importance
        FROM subcategories 
        WHERE id = @ID";

            var recordSubCategory = await _db.GetRecordsAsync<SubCategoryToEdit>(GetSubCategoryQuery, param);
            SubCategoryToEdit subCategory = recordSubCategory.FirstOrDefault();
            SubCategoryToShow subCat = new SubCategoryToShow();

            if (subCategory.id != null)
            {
                object subParam = new
                {
                    ID = subCategory.id
                };

                string GetTransactionValueQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID AND (transType = 1 OR transType = 3) ";

                var recordsTransValue = await _db.GetRecordsAsync<double>(GetTransactionValueQuery, subParam);

                subCat = new SubCategoryToShow()
                {
                    id = subCategory.id,
                    subCategoryTitle = subCategory.subCategoryTitle,
                    monthlyPlannedBudget = subCategory.monthlyPlannedBudget,
                };

                subCat.transactionsValue = recordsTransValue.FirstOrDefault();
                if (subCat != null)
                {
                    return Ok(subCat);
                }
                return BadRequest("Sub Category not found");
            }
            else
            {
                return BadRequest("no transaction in sub category");
            }
        }

    }
    
}
