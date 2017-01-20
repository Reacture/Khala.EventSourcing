using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ReactiveArchitecture.Messaging;
using TodoList.Domain.Commands;
using TodoList.ReadModel;

namespace TodoList.Controllers
{
    public class HomeController : Controller
    {
        private IReadModelFacade ReadModelFacade => Request
            .GetOwinContext()
            .Get<IReadModelFacade>(nameof(IReadModelFacade));

        private IMessageBus MessageBus => Request
            .GetOwinContext()
            .Get<IMessageBus>(nameof(IMessageBus));

        public async Task<ActionResult> Index()
        {
            return View(await ReadModelFacade.GetAllItems());
        }

        public async Task<ActionResult> Details(Guid? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            TodoItem todoItem = await ReadModelFacade.Find(id.Value);
            if (todoItem == null)
                return HttpNotFound();

            return View(todoItem);
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(
            [Bind(Include = "Description")] TodoItemCommandModel model,
            CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                var command = new CreateTodoItem(
                    Guid.NewGuid(), model.Description);
                await MessageBus.Send(command, cancellationToken);
                return RedirectToAction("Index");
            }

            return View(model);
        }

        public async Task<ActionResult> Edit(Guid? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            TodoItem todoItem = await ReadModelFacade.Find(id.Value);
            if (todoItem == null)
                return HttpNotFound();

            return View(new TodoItemCommandModel
            {
                Id = todoItem.Id,
                Description = todoItem.Description
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([
            Bind(Include = "Id,Description")] TodoItemCommandModel model,
            CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                var command = new UpdateTodoItem(model.Id, model.Description);
                await MessageBus.Send(command, cancellationToken);
                return RedirectToAction("Index");
            }

            return View(model);
        }

        public async Task<ActionResult> Delete(Guid? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            TodoItem todoItem = await ReadModelFacade.Find(id.Value);
            if (todoItem == null)
                return HttpNotFound();

            return View(todoItem);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new DeleteTodoItem(id);
            await MessageBus.Send(command, cancellationToken);
            return RedirectToAction("Index");
        }
    }
}
