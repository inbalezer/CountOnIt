﻿using CountOnIt.Shared.Models.present.toAdd;
using CountOnIt.Shared.Models.present.toEdit;
using CountOnIt.Shared.Models.present.toShow;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
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
            var userQuery = "SELECT id, firstName, profilePicOrIcon, streakStatus FROM users WHERE googleID = @ID";
            var categoryQuery = "SELECT id, categroyTitle, icon, color FROM categories WHERE userID = @ID";
            var subCategoryBudgetQuery = "SELECT COALESCE(SUM(monthlyPlannedBudget), 0) FROM subcategories WHERE categoryID = @ID";
            var transactionSumQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID AND transType = @TransType and MONTH(transDate) = MONTH(CURRENT_DATE()) AND YEAR(transDate) = YEAR(CURRENT_DATE());";

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

                        // Calculate transaction sums for the first category
                        object param = new
                        {
                            ID = category.id
                        };

                        string getSubCategoriesQuery = "select id from subcategories where categoryID=@ID";
                        var subCatsListRes = await _db.GetRecordsAsync<int>(getSubCategoriesQuery, param);
                        List<int> subCatsList = subCatsListRes.ToList();
                        if (subCatsList.Count > 0)
                        {

                            foreach (int subCatID in subCatsList)
                            {
                                user.spendingValueFullList = (await _db.GetRecordsAsync<double>(transactionSumQuery, new { ID = subCatID, TransType = 1 })).FirstOrDefault();
                                totalSpendings += user.spendingValueFullList;
                                user.incomeValueFullList = (await _db.GetRecordsAsync<double>(transactionSumQuery, new { ID = subCatID, TransType = 2 })).FirstOrDefault();
                                totalIncome += user.incomeValueFullList;
                            }
                        }



                        //}
                    }

                    // Calculate the budget usage percentage
                    user.budgetFullValue = CalculateBudgetPercentage(totalBudget, totalSpendings);
                    user.spendingValueFullList = totalSpendings;
                    user.incomeValueFullList = totalIncome;
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

        [HttpGet("checkStreak/{userID}")] //checks if the user has a current streak
        public async Task<IActionResult> CheckUserStreak(int userID)
        {
            if (userID > 0)
            {
                object param = new
                {
                    ID = userID
                };
                string getStreakQuery = "WITH valid_weeks AS (SELECT WEEK(t.transInputDate) AS week_number,YEAR(t.transInputDate) AS year_number,ROW_NUMBER() OVER (ORDER BY YEAR(t.transInputDate), WEEK(t.transInputDate)) AS rn FROM users u JOIN categories c ON u.id = c.userID JOIN subcategories sc ON c.id = sc.categoryID JOIN transactions t ON sc.id = t.subCategoryID WHERE t.transInputDate BETWEEN u.signUpDate AND CURRENT_DATE() AND u.id =@ID GROUP BY YEAR(t.transInputDate), WEEK(t.transInputDate) HAVING COUNT(*) >= 3),streaks AS (SELECT vw.*,(ROW_NUMBER() OVER (ORDER BY vw.year_number, vw.week_number) - vw.rn) AS streak_group FROM valid_weeks vw),current_streak AS (SELECT s.streak_group,COUNT(*) AS streak_length,MAX(CASE WHEN vw2.year_number = YEAR(CURRENT_DATE()) AND vw2.week_number = WEEK(CURRENT_DATE()) THEN 1 ELSE 0 END) AS is_current_streak FROM streaks s JOIN valid_weeks vw2 ON s.year_number = vw2.year_number AND s.week_number = vw2.week_number GROUP BY s.streak_group) SELECT CASE WHEN MAX(is_current_streak) = 1 THEN TRUE ELSE FALSE END AS current_streak_group,MAX(streak_length) AS streak_length FROM current_streak ORDER BY streak_length DESC LIMIT 1;";
                //gets the amount of weeks where there was a minimum of 3 transactions (in the current streak) and the streak's length

                var getStreaks = await _db.GetRecordsAsync<UserStreakData>(getStreakQuery, param);
                UserStreakData weekAmountInStreak = getStreaks.FirstOrDefault();
                if (weekAmountInStreak != null)
                {
                    return Ok(weekAmountInStreak);
                }
                return BadRequest("couldn't find streak data for this user");
            }

            return BadRequest("invalid user id");
        }

        [HttpGet("getUserStreakStatus/{userID}")] //gets the user's streak
        public async Task<IActionResult> getUserStreakStatus(int userID)
        {
            if (userID > 0)
            {
                object param = new
                {
                    ID = userID
                };
                string getStreakQuery = "SELECT streakStatus FROM users where id=@ID;";
                var getStreaks = await _db.GetRecordsAsync<string>(getStreakQuery, param);
                string streakStatus = getStreaks.FirstOrDefault();
                if (streakStatus != null)
                {
                    return Ok(streakStatus);
                }
                return BadRequest("couldn't find streak data for this user");
            }

            return BadRequest("invalid user id");
        }

        [HttpGet("getUserIcon/{userID}")] //gets the user's icon or profile pic
        public async Task<IActionResult> getUserIcon(int userID)
        {
            if (userID > 0)
            {
                object param = new
                {
                    ID = userID
                };
                string getIconQuery = "SELECT profilePicOrIcon FROM users where id=@ID;";
                var getIcon = await _db.GetRecordsAsync<string?>(getIconQuery, param);
                string? userIcon = getIcon.FirstOrDefault();

                if (userIcon == null || userIcon == "" || userIcon.Length <= 0)
                {

                    object updateParam = new
                    {
                        ID = userID,
                        profilePicOrIcon = "🌟"

                    };

                    string updateUserIconQuery = "update users set profilePicOrIcon=@profilePicOrIcon where id=@ID;";
                    bool iconUpdate = await _db.SaveDataAsync(updateUserIconQuery, updateParam);
                    if (iconUpdate)
                    {
                        string defaultIcon = "🌟";
                        return Ok(defaultIcon);
                    }

                    return BadRequest("failed to update user's icon to default");
                }
                else if (userIcon != null)
                {
                    return Ok(userIcon);
                }

                return BadRequest("couldn't find profile pic or icon for this user");
            }

            return BadRequest("invalid user id");
        }

        [HttpGet("updateStreakStat/{userID}/{newStatus}")] //updates the user's streak
        public async Task<IActionResult> updateStreakStat(int userID, string newStatus)
        {
            if (userID > 0)
            {
                object param = new
                {
                    ID = userID,
                    newStatus = newStatus
                };
                string getStreakQuery = "update users set streakStatus=@newStatus where id=@ID;";
                var StreaksUpdate = await _db.SaveDataAsync(getStreakQuery, param);

                if (StreaksUpdate)
                {
                    return Ok(StreaksUpdate);
                }
                return BadRequest("couldn't update streak status for this user");
            }

            return BadRequest("invalid user id");

        }

        [HttpGet("getTotalWeekTrans/{userID}")] //gets the total transactions done this week by the user
        public async Task<IActionResult> getTotalWeekTrans(int userID)
        {
            if (userID > 0)
            {
                object param = new
                {
                    ID = userID
                };
                string getStreakQuery = "SELECT COUNT(*) AS totalWeekTransactions FROM users u JOIN categories c ON u.id = c.userID JOIN subcategories sc ON c.id = sc.categoryID JOIN transactions t ON sc.id = t.subCategoryID WHERE u.id=@ID and WEEK(t.transInputDate, 0) = WEEK(CURRENT_DATE(), 0) AND YEAR(t.transInputDate) = YEAR(CURRENT_DATE()) AND t.transDate <= CURRENT_DATE();";
                var StreaksUpdate = await _db.GetRecordsAsync<int>(getStreakQuery, param);

                if (StreaksUpdate != null)
                {
                    return Ok(StreaksUpdate);
                }
                return BadRequest("couldn't get total amount of transactions done this week by this user");
            }

            return BadRequest("invalid user id");

        }


        [HttpGet("incomeCatId/{userID}")] //gets the ID of the income category
        public async Task<IActionResult> GetUserIncomeID(int userID)
        {
            object param = new
            {
                ID = userID
            };

            var userIncomeQuery = "SELECT c.id AS incomeID FROM categories as c, users as u where c.userID=u.id and c.categroyTitle=\"הכנסות\" and u.id=@ID";

            var getIncomeID = await _db.GetRecordsAsync<int>(userIncomeQuery, param);
            int incomeCatID = getIncomeID.FirstOrDefault();
            if (incomeCatID > 0)
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

            object updateParam;

            if (string.IsNullOrEmpty(categoryToUpdate.icon))
            {
                updateParam = new
                {
                    ID = categoryToUpdate.id,
                    categroyTitle = categoryToUpdate.categroyTitle,
                    icon = "💰",
                    color = categoryToUpdate.color
                };
            }
            else
            {
                updateParam = new
                {
                    ID = categoryToUpdate.id,
                    categroyTitle = categoryToUpdate.categroyTitle,
                    icon = categoryToUpdate.icon,
                    color = categoryToUpdate.color
                };
            }


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
            object categoryToAddParam;

            if (string.IsNullOrEmpty(categoryToAdd.icon))
            {
                categoryToAddParam = new
                {
                    userID = userID,
                    categroyTitle = categoryToAdd.categroyTitle,
                    icon = "💰",
                    color = categoryToAdd.color
                };
            }
            else
            {
                categoryToAddParam = new
                {
                    userID = userID,
                    categroyTitle = categoryToAdd.categroyTitle,
                    icon = categoryToAdd.icon,
                    color = categoryToAdd.color
                };
            }

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
                        string GetCategoryCurrentSumQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID AND MONTH(transDate) = MONTH(CURRENT_DATE()) AND YEAR(transDate) = YEAR(CURRENT_DATE())";
                        var recordSubCatCurrentSum = await _db.GetRecordsAsync<double>(GetCategoryCurrentSumQuery, subCatIDParam);
                        subCatSum += recordSubCatCurrentSum.FirstOrDefault();

                        //    // Income:
                        //    string GetIncomesQuery = "SELECT COALESCE(SUM(transValue), 0) FROM transactions WHERE subCategoryID = @ID AND transType = 2";
                        //    var recordSubCatCurrentSumIncome = await _db.GetRecordsAsync<double>(GetIncomesQuery, subCatIDParam);
                        //    subCatSum += recordSubCatCurrentSumIncome.FirstOrDefault();
                        //}

                    }
                    string GetCategoryTitleOverviewQuery = "SELECT categroyTitle FROM categories WHERE id = @ID";
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
            if (requestedSubInfo != null)
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
        public async Task<IActionResult> gettingCatID(int subCatID)
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
                return Ok(categoryTitle);
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
            object param = new { };

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

        [HttpGet("getCatColor/{catID}")]
        public async Task<IActionResult> getCatColor(int catID)
        {
            if (catID > 0)
            {
                object param = new
                {
                    ID = catID
                };
                string getColorQuery = "SELECT color FROM categories where id=@ID;";
                var catColorRec = await _db.GetRecordsAsync<string>(getColorQuery, param);
                string catColorRes = catColorRec.FirstOrDefault();
                if (catColorRes != null)
                {
                    return Ok(catColorRes);
                }
                return BadRequest("couldn't get category's color");
            }

            return BadRequest("invalid user id");

        }

        [HttpGet("checkOverdraftCats/{userID}")]
        public async Task<IActionResult> checkOverdraftCats(int userID)
        {
            object param = new { ID = userID };

            string GetCategoryOverviewIDQuery = "SELECT id FROM categories WHERE userID = @ID";
            var recordCategoryOverviewID = await _db.GetRecordsAsync<int>(GetCategoryOverviewIDQuery, param);
            List<int> catRecsList = recordCategoryOverviewID.ToList();
            if (catRecsList == null || catRecsList.Count <= 0)
            {
                return Ok(new List<CategoryOverDraftCheckToShow>());  // Return an empty list if no categories found
            }
            var usercategoriesQuery = "SELECT DISTINCT c.id FROM categories c JOIN subcategories sc ON c.id = sc.categoryID JOIN transactions t ON sc.id = t.subCategoryID WHERE t.transType = 2 and c.userID=@ID;";
            var incomeCatRec = await _db.GetRecordsAsync<int>(usercategoriesQuery, param);
            int incomeCatID = incomeCatRec.FirstOrDefault();
            if (catRecsList.Contains(incomeCatID))
            {
                int incomeIndex = catRecsList.IndexOf(incomeCatID);
                catRecsList.RemoveAt(incomeIndex);

            }
            List<CategoryOverDraftCheckToShow> checkingOverdraftingCats = new List<CategoryOverDraftCheckToShow>();

            foreach (int catID in catRecsList)
            {
                CategoryOverDraftCheckToShow currentCat = new CategoryOverDraftCheckToShow();
                double subCatSum = 0;  // Reset sum for each category
                currentCat.id = catID;
                object categoryParam = new { ID = catID };

                string GetSubCategoryIDbudget = "SELECT COALESCE(SUM(monthlyPlannedBudget), 0) FROM subcategories sc JOIN transactions t ON t.subCategoryID = sc.id JOIN categories c ON sc.categoryID = c.id WHERE c.id = @ID AND MONTH(t.transDate) = MONTH(CURRENT_DATE()) AND YEAR(t.transDate) = YEAR(CURRENT_DATE()) AND (t.transType = 1 OR t.transType = 3);";
                var recordSubCategoryID = await _db.GetRecordsAsync<double>(GetSubCategoryIDbudget, categoryParam);
                var subCatBudgetsRec = recordSubCategoryID.FirstOrDefault();

                if (subCatBudgetsRec >= 0)
                {

                    currentCat.monthlyPlannedBudget = subCatBudgetsRec;

                    // Expenses:
                    string GetCategoryCurrentSumQuery = "SELECT COALESCE(SUM(t.transValue),0) FROM transactions t JOIN subcategories sc ON t.subCategoryID = sc.id JOIN categories c ON sc.categoryID = c.id WHERE c.id = @ID AND MONTH(t.transDate) = MONTH(CURRENT_DATE()) AND YEAR(t.transDate) = YEAR(CURRENT_DATE()) AND (t.transType = 1 or t.transType=3)";
                    var recordSubCatCurrentSum = await _db.GetRecordsAsync<double>(GetCategoryCurrentSumQuery, categoryParam);
                    subCatSum = recordSubCatCurrentSum.FirstOrDefault();

                    currentCat.totalTransSum = subCatSum;
                    checkingOverdraftingCats.Add(currentCat);
                }
                return BadRequest("couldn't sum/find subcategories' budgets to a category with the id- " + catID);
            }
            return Ok(checkingOverdraftingCats);
        }


        [HttpGet("userProfileData/{userID}")]
        public async Task<IActionResult> getuserProfileData(int userID)
        {
            object param = new { ID = userID };
            string getProfileDataQuery = "SELECT * FROM users where id=@ID;";

            var userDataRes = await _db.GetRecordsAsync<userProfileDataToShow>(getProfileDataQuery, param);


            userProfileDataToShow userData = userDataRes.FirstOrDefault();
            if (userData != null)
            {
                string getUserTagsQuery = "SELECT id, tagTitle, tagColor FROM tags where userID=@ID;";
                var userTagsRes = await _db.GetRecordsAsync<TagsToShow>(getUserTagsQuery, param);
                List<TagsToShow> userTagList = userTagsRes.ToList();
                userData.userTags = userTagList;
                return Ok(userData);
            }
            return BadRequest("user is null");
        }



        [HttpPost("updateUser")]
        public async Task<IActionResult> UpdateUserData(userProfileDataToShow editedUser)
        {
            try
            {
                bool userUpdateRes = await UpdateUserDetails(editedUser);
                if (!userUpdateRes)
                {
                    return BadRequest("Failed to update user details.");
                }

                var updateResult = await UpdateUserTags(editedUser);
                if (!updateResult.IsSuccess)
                {
                    return BadRequest(updateResult.ErrorMessage);
                }

                return Ok(updateResult.Data); // Assuming you want to return the list of deleted tag IDs.
            }
            catch (Exception ex)
            {
                //_logger.LogError($"An error occurred: {ex.Message}");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        private async Task<bool> UpdateUserDetails(userProfileDataToShow editedUser)
        {
            object newUser = new
            {
                ID = editedUser.id,
                firstName = editedUser.firstName,
                lastName = editedUser.lastName,
                profilePicOrIcon = editedUser.profilePicOrIcon,
                monthStartDate = editedUser.monthStartDate
            };

            string updateUserQuery = @"update users set firstName=@firstName, lastName=@lastName, 
                               profilePicOrIcon=@profilePicOrIcon, monthStartDate=@monthStartDate 
                               where id=@ID";
            return await _db.SaveDataAsync(updateUserQuery, newUser);
        }

        private async Task<(bool IsSuccess, string ErrorMessage, List<int> Data)> UpdateUserTags(userProfileDataToShow editedUser)
        {
            string getUserTagsQuery = "select * from tags where userID=@ID";
            var userTagsRes = await _db.GetRecordsAsync<TagsToShow>(getUserTagsQuery, new { ID = editedUser.id });
            List<TagsToShow> userTagList = userTagsRes.ToList();

            // Lists to handle tags
            List<int> tagsToDelete = new List<int>();
            List<TagsToShow> tagsToUpdate = new List<TagsToShow>();
            List<TagsToShow> tagsToInsert = new List<TagsToShow>();

            foreach (var existingTag in userTagList)
            {
                var matchingTag = editedUser.userTags.FirstOrDefault(t => t.id == existingTag.id);
                if (matchingTag != null)
                {
                    if (existingTag.tagTitle != matchingTag.tagTitle || existingTag.tagColor != matchingTag.tagColor)
                    {
                        tagsToUpdate.Add(matchingTag);
                    }
                }
                else
                {
                    tagsToDelete.Add(existingTag.id);
                }
            }

            foreach (var newTag in editedUser.userTags.Where(t => t.id == 0))
            {
                tagsToInsert.Add(newTag);
            }

            // Execute tag updates
            foreach (var tag in tagsToUpdate)
            {
                if (!await UpdateTag(tag))
                {
                    return (false, $"Failed to update tag with ID {tag.id}.", null);
                }
            }

            // Insert new tags and handle errors
            foreach (var tag in tagsToInsert)
            {
                int insertedId = await InsertTag(tag, editedUser.id);
                if (insertedId <= 0)
                {
                    return (false, "Failed to insert new tag.", null);
                }
            }

            return (true, null, tagsToDelete);
        }

        private async Task<bool> UpdateTag(TagsToShow tag)
        {
            string updateTagQuery = "update tags set tagTitle=@tagTitle, tagColor=@tagColor where id=@ID";
            return await _db.SaveDataAsync(updateTagQuery, tag);
        }

        private async Task<int> InsertTag(TagsToShow tag, int userId)
        {
            string insertTagQuery = "insert into tags(tagTitle, tagColor, userID) values (@tagTitle, @tagColor, @userID)";
            return await _db.InsertReturnId(insertTagQuery, new { tagTitle = tag.tagTitle, tagColor = tag.tagColor, userID = userId });
        }

        [HttpDelete("deleteTags/tagToDelete")]
        public async Task<IActionResult> deleteTags(int tagToDelete)
        {

            object tagParam = new
            {
                tagID = tagToDelete
            };
            string updateTransTagQuery = "update transactions set tagID=null where tagID=@tagID";
            bool didTransUpdate = await _db.SaveDataAsync(updateTransTagQuery, tagParam);
            if (!didTransUpdate)
            {
                return BadRequest("failed to update tagID to null of transactions with the tagID- " + tagToDelete);
            }

            string deleteTagQuery = "delete from tags where id=@tagID";
            var deleteTag = await _db.SaveDataAsync(deleteTagQuery, tagParam);
            if (deleteTag)
            {
                return Ok("tag deleted successfully");
            }
            return BadRequest("tag wasn't deleted successfully");
        }


        [HttpPost("updateTag")]
        public async Task<IActionResult> updateTag(TagsToShow tagToUpdate)
        {
            object editedTagParam = new
            {
                id=tagToUpdate.id,
                tagTitle=tagToUpdate.tagTitle,
                tagColor=tagToUpdate.tagColor
            };
            string updateTagQuery = "update tags set tagTitle=@tagTitle, tagColor=@tagColor where id=@ID";

            bool didTagUpdate = await _db.SaveDataAsync(updateTagQuery, editedTagParam);
            if (didTagUpdate)
            {
                return Ok();
            }
            return BadRequest("failed to update tag");
        }

        [HttpPost("addTag/{userID}")]
        public async Task<IActionResult> addTag(int userID,TagsToShow tagToUpdate)
        {
            object newTagParam = new
            {
                tagTitle = tagToUpdate.tagTitle,
                tagColor = tagToUpdate.tagColor,
                userID= userID
            };
            string insertTagQuery = "insert into tags(tagTitle, tagColor, userID) values (@tagTitle, @tagColor, @userID)";

            int wasTagAdded = await _db.InsertReturnId(insertTagQuery, newTagParam);
            if (wasTagAdded>0)
            {
                return Ok();
            }
            return BadRequest("failed to add tag");
        }
    }


}


