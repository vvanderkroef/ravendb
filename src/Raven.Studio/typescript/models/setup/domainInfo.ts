/// <reference path="../../../typings/tsd.d.ts"/>

import generalUtils = require("common/generalUtils");
import checkDomainAvailabilityCommand = require("commands/setup/checkDomainAvailabilityCommand");

class domainInfo {
    private licenseProvider: () => Raven.Server.Commercial.License;
    
    domain = ko.observable<string>();
    userEmail = ko.observable<string>();
    
    availableDomains = ko.observableArray<string>([]);
    
    validationGroup: KnockoutValidationGroup;
    
    constructor(licenseProvider: () => Raven.Server.Commercial.License) {
        this.initValidation();
        this.licenseProvider = licenseProvider;
    }
    
    private initValidation() {

        const checkDomain = (val: string, params: any, callback: (currentValue: string, result: boolean) => void) => {
            new checkDomainAvailabilityCommand(val, this.licenseProvider())
                .execute()
                .done(result => {
                    callback(this.domain(), result.available || result.isOwnedByMe); //TODO: upper case 
                });
        };
        
        this.domain.extend({
            required: true,
            validation: {
                message: "Sorry, domain name is taken.",
                async: true,
                onlyIf: () => !!this.domain(),
                validator: generalUtils.debounceAndFunnel(checkDomain)
            }
        });
        
        this.userEmail.extend({
            required: true,
            email: true
        });
        
        this.validationGroup = ko.validatedObservable({
            domain: this.domain,
            userEmail: this.userEmail
        });
    }
    
    setDomain(value: string) {
        this.domain(value);
    }
}

export = domainInfo;
