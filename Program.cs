using Microsoft.EntityFrameworkCore;
using TodoApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Authorization;
var builder = WebApplication.CreateBuilder(args);
//הזרקת תלויות
builder.Services.AddDbContext<ToDoDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("ToDoDB"),
    ServerVersion.Parse("8.0-mysql")));
//swagger
builder.Services.AddOpenApi();
//cors
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("http://localhost:3000")
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});
//הוספת אופצית הזדהות בסווגר
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Bearer Authentication with JWT Token",
        Type = SecuritySchemeType.Http
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
        Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                }
            },
            new List<string>()
        }
    });
});



//JWT token
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
     options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
 })
 .AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer =builder.Configuration.GetValue<string>("Jwt:Issuer"),
        ValidAudience = builder.Configuration.GetValue<string>("Jwt:Audience"),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});
builder.Services.AddControllers();
var app = builder.Build();
app.UseCors("AllowSpecificOrigin");
// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
//jwt
app.UseRouting();
app.UseAuthentication(); 
app.UseAuthorization();
app.MapControllers();
//שליפת כל המשימות 
app.MapGet("/", (ToDoDbContext db) => "its running!!!!!!!😁😁😁");
// app.MapGet("/", (ToDoDbContext db) => db.Items.ToListAsync());
//שליפה ע"פ מזהה של משתמש
app.MapGet("/byId", [Authorize] async (ToDoDbContext db, int id) => 
{
    return await db.Items.Where(x => x.UserId == id).ToListAsync();
});
//הוספת משימה למשתמש הנוכחי
app.MapPost("/", (ToDoDbContext db, string? name,int? usId) =>
{
    Item newItem = new Item(){Name=name,UserId=usId};
    db.Items.Add(newItem);
    var i = db.SaveChangesAsync();
    return i;
});
//עדכון משימה
app.MapPut("/",async (ToDoDbContext db, int id) =>
{
    var item = db.Items.FirstOrDefault(i => i.Id == id);
    if (item == null)
        return null;
    item.IsComplete = !item.IsComplete;
    var i =await db.SaveChangesAsync();
    return item.IsComplete!;
});
//מחיקת משימה
app.MapDelete("/",async  (ToDoDbContext db, int id) =>
{
    var item = db.Items.FirstOrDefault(i => i.Id == id);
    if (item != null)
    {
        db.Items.Remove(item);
        int i =await db.SaveChangesAsync();
        return i;
    }
    return -1;
});
//יצירת טוקן
   object createJwt(User user)
{
    var claims = new List<Claim>()
    {
        new Claim("name", user.Name),
                new Claim("id", user.Id.ToString()),
                new Claim("password", user.Password),
    };
    var secretKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetValue<string>("Jwt:key")));
    var siginicCredentails=new SigningCredentials(secretKey,SecurityAlgorithms.HmacSha256);
    var tokenOption=new JwtSecurityToken(
        issuer: builder.Configuration.GetValue<string>("Jwt:Issuer"),
        audience:builder.Configuration.GetValue<string>("Jwt:Audience"),
        claims: claims,
        expires: DateTime.Now.AddMinutes(30),
        signingCredentials: siginicCredentails);
        var tokenString=new JwtSecurityTokenHandler().WriteToken(tokenOption);
    return new {Token=tokenString};
}
//התחברות
app.MapPost("/login", async (ToDoDbContext db, User user) =>
{
 var myUser= db.Users.Where(x=>x.Id == user.Id).ToList();
if (myUser.Count()>0 && myUser[0].Password == user.Password){
    var  jwt= createJwt(myUser[0]);
    return Results.Ok(new {jwt,myUser});
 }
 //--שגיאת 401-----
return Results.Unauthorized();
});
//ללא אטריביוט של טוקן
//הרשמה

app.MapPost("/addUser",async (ToDoDbContext db, User user) =>
{
var myUser= db.Users.Where(x=>x.Id == user.Id);
if (myUser.Count()==0){
    await db.Users.AddAsync(user);
    await db.SaveChangesAsync();
    var jwt= createJwt(user);
    return Results.Ok(jwt);
}
//--שגיאת 401-----
return Results.Unauthorized();
});



//שליפת המשתמשים
app.MapGet("/users", (ToDoDbContext db) => db.Users.ToListAsync());
//מידע על האפליקציה- אמור ליהות אמיתי
//ללא אטריביוט של טוקן
app.MapGet("/info", () => "פרויקט פרקטיקוד 3\nיוצר: אסתי קרליבך ");
app.Run();
