import commandBase = require("commands/commandBase");
import database = require("models/database");

class startIndexingCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<any> {
      this.reportInfo("Enabling indexing...");

        var url = '/admin/startIndexing';
        var createTask = this.post(url, null, this.db);
        createTask.done(() => this.reportSuccess("Indexing was enabled"));
        createTask.fail((response) => this.reportError("Failed to enable indexing", JSON.stringify(response)));

        return createTask;
    }
}

export = startIndexingCommand;